using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace ArbuzTweaker;

public sealed class IntelXtuService
{
    public const string PackageId = "Intel.IntelExtremeTuningUtility";
    public const string OfficialPageUrl = "https://www.intel.com/content/www/us/en/download/17881/intel-extreme-tuning-utility-intel-xtu.html";
    private static readonly TimeSpan WingetTimeout = TimeSpan.FromMinutes(10);

    public bool IsInstalled => FindInstalledEntry() != null;

    public string InstalledVersion
    {
        get
        {
            var entry = FindInstalledEntry();
            return entry?.DisplayVersion ?? "неизвестно";
        }
    }

    public async Task<ThirdPartyToolInstallResult> InstallOrUpdateAsync()
    {
        if (IsInstalled)
            return ThirdPartyToolInstallResult.Success($"Intel XTU уже установлен ({InstalledVersion}). Для выбора другой версии откройте официальную страницу Intel XTU.");

        var arguments = new StringBuilder();
        arguments.Append("install");
        arguments.Append(" --id ").Append(PackageId);
        arguments.Append(" -e --source winget");
        arguments.Append(" --accept-package-agreements --accept-source-agreements");
        arguments.Append(" --disable-interactivity --silent");

        var result = await RunWingetAsync(arguments.ToString());
        if (!result.IsSuccess)
            return ThirdPartyToolInstallResult.Failure(result.Message);

        return ThirdPartyToolInstallResult.Success($"Intel XTU установлен ({InstalledVersion}).");
    }

    public bool OpenInstallFolder()
    {
        var entry = FindInstalledEntry();
        var folder = entry?.InstallLocation;

        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            folder = TryResolveFolderFromDisplayIcon(entry?.DisplayIcon);

        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return false;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{folder}\"",
                UseShellExecute = true
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool OpenOfficialPage()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = OfficialPageUrl,
                UseShellExecute = true
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<ThirdPartyToolInstallResult> RunWingetAsync(string arguments)
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "winget",
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            if (!process.Start())
                return ThirdPartyToolInstallResult.Failure("Не удалось запустить winget.");

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            var waitTask = process.WaitForExitAsync();
            var completedTask = await Task.WhenAny(waitTask, Task.Delay(WingetTimeout));
            if (completedTask != waitTask)
            {
                TryKillProcess(process);
                return ThirdPartyToolInstallResult.Failure("winget не завершился за отведённое время.");
            }

            await waitTask;

            var output = await outputTask;
            var error = await errorTask;
            var combined = string.Join(Environment.NewLine, new[] { output, error }).Trim();

            if (process.ExitCode == 0)
                return ThirdPartyToolInstallResult.Success(string.IsNullOrWhiteSpace(combined) ? "Операция выполнена успешно." : combined);

            return ThirdPartyToolInstallResult.Failure(string.IsNullOrWhiteSpace(combined) ? "winget вернул ошибку." : combined);
        }
        catch (Exception ex)
        {
            return ThirdPartyToolInstallResult.Failure($"Не удалось запустить winget: {ex.Message}");
        }
    }

    private static void TryKillProcess(Process process)
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

    private InstalledProgramEntry? FindInstalledEntry()
    {
        return FindInstalledEntryInRoot(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall")
            ?? FindInstalledEntryInRoot(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall")
            ?? FindInstalledEntryInRoot(Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
    }

    private InstalledProgramEntry? FindInstalledEntryInRoot(RegistryKey root, string path)
    {
        using var uninstallKey = root.OpenSubKey(path, false);
        if (uninstallKey == null)
            return null;

        foreach (var subKeyName in uninstallKey.GetSubKeyNames())
        {
            using var subKey = uninstallKey.OpenSubKey(subKeyName, false);
            if (subKey == null)
                continue;

            var displayName = subKey.GetValue("DisplayName") as string;
            if (string.IsNullOrWhiteSpace(displayName) || !IsIntelXtuDisplayName(displayName))
                continue;

            return new InstalledProgramEntry
            {
                DisplayName = displayName,
                DisplayVersion = subKey.GetValue("DisplayVersion") as string,
                InstallLocation = subKey.GetValue("InstallLocation") as string,
                DisplayIcon = subKey.GetValue("DisplayIcon") as string
            };
        }

        return null;
    }

    private static bool IsIntelXtuDisplayName(string displayName)
    {
        return displayName.Contains("Intel", StringComparison.OrdinalIgnoreCase)
            && (displayName.Contains("Extreme Tuning Utility", StringComparison.OrdinalIgnoreCase)
                || displayName.Contains("XTU", StringComparison.OrdinalIgnoreCase));
    }

    private static string? TryResolveFolderFromDisplayIcon(string? displayIcon)
    {
        if (string.IsNullOrWhiteSpace(displayIcon))
            return null;

        var cleanPath = displayIcon.Trim().Trim('"');
        var commaIndex = cleanPath.IndexOf(',');
        if (commaIndex >= 0)
            cleanPath = cleanPath[..commaIndex];

        if (!File.Exists(cleanPath))
            return null;

        return Path.GetDirectoryName(cleanPath);
    }

    private sealed class InstalledProgramEntry
    {
        public string? DisplayName { get; init; }

        public string? DisplayVersion { get; init; }

        public string? InstallLocation { get; init; }

        public string? DisplayIcon { get; init; }
    }
}
