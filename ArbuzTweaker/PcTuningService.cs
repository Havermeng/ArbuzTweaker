using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace ArbuzTweaker;

/// <summary>
/// Применяет твики из <see cref="PcTuningCatalog"/>. Каждое изменение сначала уходит
/// в бэкап реестра, поэтому любой твик отсюда откатывается кнопкой отката.
/// </summary>
public sealed class PcTuningService
{
    private const string BackupGroup = "PC-Tuning";
    private const string HibernatePowerKeyPath = @"SYSTEM\CurrentControlSet\Control\Power";
    private const string NetBtInterfacesKeyPath = @"SYSTEM\CurrentControlSet\Services\NetBT\Parameters\Interfaces";
    private const string DisplayClassKeyPath = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
    private const int ProcessTimeoutMilliseconds = 60000;

    private readonly RegistryBackupService? _backupService;
    private readonly AppLogService? _logService;

    public PcTuningService(RegistryBackupService? backupService = null, AppLogService? logService = null)
    {
        _backupService = backupService;
        _logService = logService;
    }

    public static int CurrentWindowsBuild
    {
        get
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", false);
                var raw = key?.GetValue("CurrentBuildNumber") as string;
                return int.TryParse(raw, out var build) ? build : Environment.OSVersion.Version.Build;
            }
            catch
            {
                return Environment.OSVersion.Version.Build;
            }
        }
    }

    public IReadOnlyList<PcTuningTweak> GetSupportedTweaks()
    {
        var build = CurrentWindowsBuild;
        return PcTuningCatalog.All.Where(tweak => tweak.IsSupported(build)).ToList();
    }

    /// <summary>Определяет, применён ли твик сейчас. Для составных твиков — только если применены все значения.</summary>
    public bool IsEnabled(PcTuningTweak tweak)
    {
        try
        {
            return tweak.Action switch
            {
                PcTuningAction.Hibernation => IsHibernationDisabled(),
                PcTuningAction.DevicePowerSaving => IsDevicePowerSavingDisabled(),
                PcTuningAction.NetBios => IsNetBiosDisabled(),
                PcTuningAction.NvidiaPState => IsNvidiaPStateDisabled(),
                PcTuningAction.CompactOs => IsCompactOsEnabled(),
                PcTuningAction.UltimatePlan => IsUltimatePlanActive(),
                PcTuningAction.CoreUnpark => IsCoreUnparked(),
                PcTuningAction.Nagle => IsNagleDisabled(),
                PcTuningAction.NicOffloads => AreNicOffloadsDisabled(),
                PcTuningAction.MaxCpuPerformance => IsMaxCpuPerformanceEnabled(),
                _ => AreRegistryValuesApplied(tweak)
            };
        }
        catch (Exception ex)
        {
            _logService?.Error($"Failed to read PC-Tuning tweak state: {tweak.Id}", ex);
            return false;
        }
    }

    public async Task<bool> ApplyAsync(PcTuningTweak tweak, bool enable)
    {
        try
        {
            switch (tweak.Action)
            {
                case PcTuningAction.Hibernation:
                    return await SetHibernationAsync(enable);
                case PcTuningAction.DevicePowerSaving:
                    return await SetDevicePowerSavingAsync(enable);
                case PcTuningAction.NetBios:
                    return await Task.Run(() => SetNetBios(enable));
                case PcTuningAction.NvidiaPState:
                    return await Task.Run(() => SetNvidiaPState(enable));
                case PcTuningAction.CompactOs:
                    return await SetCompactOsAsync(enable);
                case PcTuningAction.UltimatePlan:
                    return await SetUltimatePlanAsync(enable);
                case PcTuningAction.CoreUnpark:
                    return await SetCoreUnparkAsync(enable);
                case PcTuningAction.Nagle:
                    return await Task.Run(() => SetNagle(enable));
                case PcTuningAction.NicOffloads:
                    return await Task.Run(() => SetNicOffloads(enable));
                case PcTuningAction.MaxCpuPerformance:
                    return await SetMaxCpuPerformanceAsync(enable);
                default:
                    return await Task.Run(() => ApplyRegistryValues(tweak, enable));
            }
        }
        catch (Exception ex)
        {
            _logService?.Error($"Failed to apply PC-Tuning tweak: {tweak.Id}", ex);
            return false;
        }
    }

    private static bool AreRegistryValuesApplied(PcTuningTweak tweak)
    {
        if (tweak.Values.Count == 0)
            return false;

        foreach (var value in tweak.Values)
        {
            using var key = OpenKey(value.Root, value.KeyPath, false);
            if (!value.Matches(key?.GetValue(value.Name)))
                return false;
        }

        return true;
    }

    private bool ApplyRegistryValues(PcTuningTweak tweak, bool enable)
    {
        var failed = false;

        foreach (var value in tweak.Values)
        {
            try
            {
                _backupService?.CaptureValue(BackupGroup, GetRootName(value.Root), value.KeyPath, value.Name);

                if (!WriteValue(value, enable))
                    failed = true;
            }
            catch (Exception ex)
            {
                failed = true;
                _logService?.Error($"Failed to write {GetRootName(value.Root)}\\{value.KeyPath}\\{value.Name}", ex);
            }
        }

        return !failed;
    }

    /// <summary>
    /// Пишет одно значение. Сначала обычным путём; если HKLM-запись отклонена
    /// (раздел во владении TrustedInstaller), берёт раздел во владение и пишет так.
    /// </summary>
    private bool WriteValue(PcTuningValue value, bool enable)
    {
        try
        {
            using var key = CreateKey(value.Root, value.KeyPath);
            if (key == null)
                throw new UnauthorizedAccessException();

            ApplyValue(key, value, enable);
            return true;
        }
        catch (Exception ex) when (value.Root == PcTuningRoot.LocalMachine
            && (ex is UnauthorizedAccessException or System.Security.SecurityException))
        {
            var ok = enable
                ? RegistryElevation.SetValue(value.KeyPath, value.Name, value.EnabledValue, value.ValueKind)
                : value.DisabledValue != null
                    ? RegistryElevation.SetValue(value.KeyPath, value.Name, value.DisabledValue, value.ValueKind)
                    : RegistryElevation.DeleteValue(value.KeyPath, value.Name);

            if (ok)
                _logService?.Info($"Wrote protected value by taking ownership: HKLM\\{value.KeyPath}\\{value.Name}");
            else
                _logService?.Error($"Failed to write even after taking ownership: HKLM\\{value.KeyPath}\\{value.Name}");

            return ok;
        }
    }

    private static void ApplyValue(RegistryKey key, PcTuningValue value, bool enable)
    {
        if (enable)
        {
            key.SetValue(value.Name, value.EnabledValue, value.ValueKind);
        }
        else if (value.DisabledValue != null)
        {
            key.SetValue(value.Name, value.DisabledValue, value.ValueKind);
        }
        else if (key.GetValue(value.Name) != null)
        {
            // Значения по умолчанию не было — откат означает удаление параметра.
            key.DeleteValue(value.Name, false);
        }
    }

    // ───────────── Гибернация ─────────────

    private static bool IsHibernationDisabled()
    {
        using var key = Registry.LocalMachine.OpenSubKey(HibernatePowerKeyPath, false);
        return key?.GetValue("HibernateEnabled") is int enabled && enabled == 0;
    }

    private async Task<bool> SetHibernationAsync(bool disable)
    {
        _backupService?.CaptureValue(BackupGroup, "HKLM", HibernatePowerKeyPath, "HibernateEnabled");
        _backupService?.CaptureValue(BackupGroup, "HKLM", @"SYSTEM\CurrentControlSet\Control\Session Manager\Power", "HiberbootEnabled");

        var result = await RunProcessAsync("powercfg.exe", disable ? "/hibernate off" : "/hibernate on");
        return result;
    }

    // ───────────── Энергосбережение устройств ─────────────

    private bool IsDevicePowerSavingDisabled()
    {
        // Считаем устройства, которым Windows ещё разрешает засыпать.
        var output = RunPowerShellQuery(
            "@(Get-CimInstance -Namespace root/wmi -ClassName MSPower_DeviceEnable -ErrorAction SilentlyContinue | Where-Object { $_.Enable }).Count");

        return int.TryParse(output?.Trim(), out var enabledCount) && enabledCount == 0;
    }

    private async Task<bool> SetDevicePowerSavingAsync(bool disable)
    {
        var target = disable ? "$false" : "$true";
        var script =
            "$devices = Get-CimInstance -Namespace root/wmi -ClassName MSPower_DeviceEnable -ErrorAction SilentlyContinue; " +
            "if (-not $devices) { exit 1 }; " +
            "foreach ($device in $devices) { try { $device.Enable = " + target + "; Set-CimInstance -CimInstance $device -ErrorAction Stop } catch { } }; " +
            "exit 0";

        return await RunPowerShellAsync(script);
    }

    // ───────────── NetBIOS over TCP/IP ─────────────

    private static bool IsNetBiosDisabled()
    {
        using var interfacesKey = Registry.LocalMachine.OpenSubKey(NetBtInterfacesKeyPath, false);
        if (interfacesKey == null)
            return false;

        var interfaceNames = interfacesKey.GetSubKeyNames();
        if (interfaceNames.Length == 0)
            return false;

        foreach (var interfaceName in interfaceNames)
        {
            using var interfaceKey = interfacesKey.OpenSubKey(interfaceName, false);
            if (interfaceKey?.GetValue("NetbiosOptions") is not int option || option != 2)
                return false;
        }

        return true;
    }

    private bool SetNetBios(bool disable)
    {
        using var interfacesKey = Registry.LocalMachine.OpenSubKey(NetBtInterfacesKeyPath, true);
        if (interfacesKey == null)
            return false;

        var failed = false;

        foreach (var interfaceName in interfacesKey.GetSubKeyNames())
        {
            try
            {
                var interfacePath = NetBtInterfacesKeyPath + "\\" + interfaceName;
                _backupService?.CaptureValue(BackupGroup, "HKLM", interfacePath, "NetbiosOptions");

                using var interfaceKey = interfacesKey.OpenSubKey(interfaceName, true);
                // 2 — выключить NetBIOS, 0 — режим по умолчанию (решает DHCP).
                interfaceKey?.SetValue("NetbiosOptions", disable ? 2 : 0, RegistryValueKind.DWord);
            }
            catch (Exception ex)
            {
                failed = true;
                _logService?.Error($"Failed to set NetbiosOptions for {interfaceName}", ex);
            }
        }

        return !failed;
    }

    // ───────────── NVIDIA P-State ─────────────

    private bool IsNvidiaPStateDisabled()
    {
        var subKeys = GetNvidiaClassSubKeys();
        if (subKeys.Count == 0)
            return false;

        foreach (var subKeyPath in subKeys)
        {
            using var key = Registry.LocalMachine.OpenSubKey(subKeyPath, false);
            if (key?.GetValue("DisableDynamicPstate") is not int value || value != 1)
                return false;
        }

        return true;
    }

    private bool SetNvidiaPState(bool disable)
    {
        var subKeys = GetNvidiaClassSubKeys();
        if (subKeys.Count == 0)
            return false;

        var failed = false;

        foreach (var subKeyPath in subKeys)
        {
            try
            {
                _backupService?.CaptureValue(BackupGroup, "HKLM", subKeyPath, "DisableDynamicPstate");

                using var key = Registry.LocalMachine.OpenSubKey(subKeyPath, true);
                if (key == null)
                {
                    failed = true;
                    continue;
                }

                if (disable)
                    key.SetValue("DisableDynamicPstate", 1, RegistryValueKind.DWord);
                else if (key.GetValue("DisableDynamicPstate") != null)
                    key.DeleteValue("DisableDynamicPstate", false); // По умолчанию параметра нет — откат = удаление.
            }
            catch (Exception ex)
            {
                failed = true;
                _logService?.Error($"Failed to set DisableDynamicPstate for {subKeyPath}", ex);
            }
        }

        return !failed;
    }

    /// <summary>Пути подключей класса «Display adapters», принадлежащих видеокартам NVIDIA.</summary>
    private static List<string> GetNvidiaClassSubKeys()
    {
        var result = new List<string>();
        using var classKey = Registry.LocalMachine.OpenSubKey(DisplayClassKeyPath, false);
        if (classKey == null)
            return result;

        foreach (var subKeyName in classKey.GetSubKeyNames())
        {
            // Только числовые подключи вида 0000/0001 — остальное (Properties и т.п.) пропускаем.
            if (subKeyName.Length != 4 || !subKeyName.All(char.IsDigit))
                continue;

            using var subKey = classKey.OpenSubKey(subKeyName, false);
            if (subKey == null)
                continue;

            var provider = subKey.GetValue("ProviderName") as string ?? string.Empty;
            var matchingId = subKey.GetValue("MatchingDeviceId") as string ?? string.Empty;

            if (provider.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase)
                || matchingId.Contains("VEN_10DE", StringComparison.OrdinalIgnoreCase))
            {
                result.Add(DisplayClassKeyPath + "\\" + subKeyName);
            }
        }

        return result;
    }

    // ───────────── CompactOS ─────────────

    private bool IsCompactOsEnabled()
    {
        var output = RunProcessOutput("compact.exe", "/compactos:query")?.ToLowerInvariant();
        if (string.IsNullOrEmpty(output))
            return false;

        // Явно выключено — англ. формулировка; иначе считаем включённым только при явном признаке.
        if (output.Contains("not in the compact"))
            return false;

        return output.Contains("in the compact state");
    }

    private async Task<bool> SetCompactOsAsync(bool enable)
    {
        return await RunProcessAsync("compact.exe", enable ? "/compactos:always" : "/compactos:never");
    }

    // ───────────── План питания «Максимальная производительность» ─────────────

    private const string BalancedSchemeGuid = "381b4222-f694-41f0-9685-ff5bb260df2e";
    private const string UltimateTemplateGuid = "e9a42b02-d5df-448d-aa00-03f14749eb61";
    private static readonly string[] UltimateSchemeNames = { "ultimate", "максимальная производительность" };

    private static string? GetActiveSchemeGuid()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes", false);
        return key?.GetValue("ActivePowerScheme") as string;
    }

    private string? GetUltimateSchemeGuid()
    {
        var output = RunProcessOutput("powercfg.exe", "/list");
        if (output == null)
            return null;

        foreach (var line in output.Split('\n'))
        {
            var lower = line.ToLowerInvariant();
            if (!UltimateSchemeNames.Any(lower.Contains))
                continue;

            var match = System.Text.RegularExpressions.Regex.Match(
                line, "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
            if (match.Success)
                return match.Value;
        }

        return null;
    }

    private bool IsUltimatePlanActive()
    {
        var active = GetActiveSchemeGuid();
        var ultimate = GetUltimateSchemeGuid();
        return active != null && ultimate != null && string.Equals(active, ultimate, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<bool> SetUltimatePlanAsync(bool enable)
    {
        if (!enable)
            return await RunProcessAsync("powercfg.exe", "/setactive " + BalancedSchemeGuid);

        var guid = GetUltimateSchemeGuid();
        if (guid == null)
        {
            await RunProcessAsync("powercfg.exe", "-duplicatescheme " + UltimateTemplateGuid);
            guid = GetUltimateSchemeGuid();
        }

        if (guid == null)
            return false;

        return await RunProcessAsync("powercfg.exe", "/setactive " + guid);
    }

    // ───────────── Распарковка ядер ─────────────

    private const string ProcessorSubGroupGuid = "54533251-82be-4824-96c1-47b60b740d00";
    private const string CoreParkingMinSetting = "0cc5b647-c1df-4637-891a-dec35c318583"; // CPMINCORES

    private bool IsCoreUnparked()
    {
        try
        {
            var active = GetActiveSchemeGuid();
            if (active == null)
                return false;

            var path = $@"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes\{active}\{ProcessorSubGroupGuid}\{CoreParkingMinSetting}";
            using var key = Registry.LocalMachine.OpenSubKey(path, false);
            return key?.GetValue("ACSettingIndex") is int index && index == 100;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> SetCoreUnparkAsync(bool enable)
    {
        // 100 = все ядра всегда активны; 10 = вернуть парковку (значение по умолчанию у разных ПК разное,
        // точный откат делает кнопка бэкапа реестра).
        var value = enable ? "100" : "10";
        var ok = await RunProcessAsync("powercfg.exe", $"-setacvalueindex scheme_current sub_processor {CoreParkingMinSetting} {value}");
        ok &= await RunProcessAsync("powercfg.exe", $"-setdcvalueindex scheme_current sub_processor {CoreParkingMinSetting} {value}");
        ok &= await RunProcessAsync("powercfg.exe", "-setactive scheme_current");
        return ok;
    }

    // ───────────── Максимальная частота процессора ─────────────

    private const string ProcThrottleMinSetting = "893dee8e-2bef-41e0-89c6-b55d0929964c"; // PROCTHROTTLEMIN
    private const string PerfBoostModeSetting = "be337238-0d82-4146-a960-4f3749d470c7";   // PERFBOOSTMODE

    private bool IsMaxCpuPerformanceEnabled()
    {
        try
        {
            var active = GetActiveSchemeGuid();
            if (active == null)
                return false;

            var path = $@"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes\{active}\{ProcessorSubGroupGuid}\{ProcThrottleMinSetting}";
            using var key = Registry.LocalMachine.OpenSubKey(path, false);
            return key?.GetValue("ACSettingIndex") is int index && index == 100;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> SetMaxCpuPerformanceAsync(bool enable)
    {
        // 100 = процессор всегда на полной частоте; 5 = обычный минимум по умолчанию
        // (точное исходное значение у разных ПК своё, полный откат — через бэкап реестра).
        var minState = enable ? "100" : "5";
        var ok = await RunProcessAsync("powercfg.exe", $"-setacvalueindex scheme_current sub_processor {ProcThrottleMinSetting} {minState}");
        ok &= await RunProcessAsync("powercfg.exe", $"-setdcvalueindex scheme_current sub_processor {ProcThrottleMinSetting} {minState}");
        // Режим ускорения «Агрессивный» (2) — штатное значение Windows, ставим и при откате.
        ok &= await RunProcessAsync("powercfg.exe", $"-setacvalueindex scheme_current sub_processor {PerfBoostModeSetting} 2");
        ok &= await RunProcessAsync("powercfg.exe", $"-setdcvalueindex scheme_current sub_processor {PerfBoostModeSetting} 2");
        ok &= await RunProcessAsync("powercfg.exe", "-setactive scheme_current");
        return ok;
    }

    // ───────────── Алгоритм Нагла ─────────────

    private const string TcpipInterfacesKeyPath = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces";

    private static bool IsNagleDisabled()
    {
        using var key = Registry.LocalMachine.OpenSubKey(TcpipInterfacesKeyPath, false);
        var names = key?.GetSubKeyNames();
        if (names == null || names.Length == 0)
            return false;

        foreach (var name in names)
        {
            using var sub = key!.OpenSubKey(name, false);
            if (sub?.GetValue("TcpAckFrequency") is not int frequency || frequency != 1)
                return false;
        }

        return true;
    }

    private bool SetNagle(bool disable)
    {
        using var key = Registry.LocalMachine.OpenSubKey(TcpipInterfacesKeyPath, true);
        if (key == null)
            return false;

        var failed = false;
        foreach (var name in key.GetSubKeyNames())
        {
            try
            {
                var interfacePath = TcpipInterfacesKeyPath + "\\" + name;
                _backupService?.CaptureValue(BackupGroup, "HKLM", interfacePath, "TcpAckFrequency");
                _backupService?.CaptureValue(BackupGroup, "HKLM", interfacePath, "TCPNoDelay");

                using var sub = key.OpenSubKey(name, true);
                if (sub == null)
                    continue;

                if (disable)
                {
                    sub.SetValue("TcpAckFrequency", 1, RegistryValueKind.DWord);
                    sub.SetValue("TCPNoDelay", 1, RegistryValueKind.DWord);
                }
                else
                {
                    if (sub.GetValue("TcpAckFrequency") != null)
                        sub.DeleteValue("TcpAckFrequency", false);
                    if (sub.GetValue("TCPNoDelay") != null)
                        sub.DeleteValue("TCPNoDelay", false);
                }
            }
            catch (Exception ex)
            {
                failed = true;
                _logService?.Error($"Failed to set Nagle for {name}", ex);
            }
        }

        return !failed;
    }

    // ───────────── Энергосбережение сетевых адаптеров ─────────────

    private const string NetClassKeyPath = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}";

    // Имя параметра → значение «включено» (для отката). Значения — строки (REG_SZ).
    private static readonly (string Name, string OnValue)[] NicOffloadValues =
    {
        ("*EEE", "1"),
        ("EnableGreenEthernet", "1"),
        ("*FlowControl", "3"),
        ("*InterruptModeration", "1")
    };

    private static List<string> GetNicAdapterKeys()
    {
        var result = new List<string>();
        using var classKey = Registry.LocalMachine.OpenSubKey(NetClassKeyPath, false);
        if (classKey == null)
            return result;

        foreach (var name in classKey.GetSubKeyNames())
        {
            if (name.Length != 4 || !name.All(char.IsDigit))
                continue;

            using var sub = classKey.OpenSubKey(name, false);
            if (sub == null)
                continue;

            // Только адаптеры, у которых реально есть хотя бы один из этих параметров.
            if (NicOffloadValues.Any(value => sub.GetValue(value.Name) != null))
                result.Add(NetClassKeyPath + "\\" + name);
        }

        return result;
    }

    private static bool AreNicOffloadsDisabled()
    {
        var keys = GetNicAdapterKeys();
        if (keys.Count == 0)
            return false;

        foreach (var keyPath in keys)
        {
            using var key = Registry.LocalMachine.OpenSubKey(keyPath, false);
            if (key == null)
                continue;

            foreach (var (name, _) in NicOffloadValues)
            {
                if (key.GetValue(name) is string value && value != "0")
                    return false;
            }
        }

        return true;
    }

    private bool SetNicOffloads(bool disable)
    {
        var keys = GetNicAdapterKeys();
        if (keys.Count == 0)
            return false;

        var failed = false;
        foreach (var keyPath in keys)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(keyPath, true);
                if (key == null)
                    continue;

                foreach (var (name, onValue) in NicOffloadValues)
                {
                    if (key.GetValue(name) == null)
                        continue; // не создаём отсутствующие параметры

                    _backupService?.CaptureValue(BackupGroup, "HKLM", keyPath, name);
                    key.SetValue(name, disable ? "0" : onValue, RegistryValueKind.String);
                }
            }
            catch (Exception ex)
            {
                failed = true;
                _logService?.Error($"Failed to set NIC offloads for {keyPath}", ex);
            }
        }

        return !failed;
    }

    private string? RunProcessOutput(string fileName, string arguments)
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();

            if (!process.WaitForExit(ProcessTimeoutMilliseconds))
            {
                TryKill(process);
                return null;
            }

            return output;
        }
        catch (Exception ex)
        {
            _logService?.Error($"Failed to run {fileName}", ex);
            return null;
        }
    }

    // ───────────── Запуск процессов ─────────────

    private async Task<bool> RunPowerShellAsync(string script)
    {
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        return await RunProcessAsync("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -EncodedCommand {encoded}");
    }

    private async Task<bool> RunProcessAsync(string fileName, string arguments)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                process.Start();
                process.StandardOutput.ReadToEnd();
                process.StandardError.ReadToEnd();

                if (!process.WaitForExit(ProcessTimeoutMilliseconds))
                {
                    TryKill(process);
                    return false;
                }

                return process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                _logService?.Error($"Failed to run {fileName}", ex);
                return false;
            }
        });
    }

    private string? RunPowerShellQuery(string script)
    {
        try
        {
            var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -EncodedCommand {encoded}",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();

            if (!process.WaitForExit(ProcessTimeoutMilliseconds))
            {
                TryKill(process);
                return null;
            }

            return output;
        }
        catch (Exception ex)
        {
            _logService?.Error("Failed to run PowerShell query.", ex);
            return null;
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(true);
        }
        catch
        {
        }
    }

    private static RegistryKey? OpenKey(PcTuningRoot root, string keyPath, bool writable)
    {
        return root == PcTuningRoot.CurrentUser
            ? Registry.CurrentUser.OpenSubKey(keyPath, writable)
            : Registry.LocalMachine.OpenSubKey(keyPath, writable);
    }

    private static RegistryKey? CreateKey(PcTuningRoot root, string keyPath)
    {
        return root == PcTuningRoot.CurrentUser
            ? Registry.CurrentUser.CreateSubKey(keyPath, true)
            : Registry.LocalMachine.CreateSubKey(keyPath, true);
    }

    private static string GetRootName(PcTuningRoot root) => root == PcTuningRoot.CurrentUser ? "HKCU" : "HKLM";
}
