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

                using var key = CreateKey(value.Root, value.KeyPath);
                if (key == null)
                {
                    failed = true;
                    continue;
                }

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
            catch (Exception ex)
            {
                failed = true;
                _logService?.Error($"Failed to write {GetRootName(value.Root)}\\{value.KeyPath}\\{value.Name}", ex);
            }
        }

        return !failed;
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
