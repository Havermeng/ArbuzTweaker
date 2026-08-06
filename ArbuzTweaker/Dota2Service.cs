using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace ArbuzTweaker;

public class Dota2Service
{
    public const string AutoexecFileName = "autoexec.cfg";
    public const string AutoexecLaunchCommand = "+exec autoexec.cfg";
    public const string LegacyAutoexecFileName = "autoexec.cfg.txt";
    public const string LegacyAutoexecLaunchCommand = "+exec autoexec.cfg.txt";

    private string? _dotaPath;
    private string? _steamPath;
    private readonly FileBackupService? _fileBackupService;
    private readonly AppLogService? _logService;

    public Dota2Service()
    {
    }

    public Dota2Service(FileBackupService fileBackupService, AppLogService logService)
    {
        _fileBackupService = fileBackupService;
        _logService = logService;
    }

    public string? DotaPath => _dotaPath;
    public string? SteamPath => _steamPath;
    public string? PreferredSteamAccountId32 { get; set; }
    public Func<string?>? PreferredSteamAccountResolver { get; set; }

    private string? ResolvePreferredSteamAccountId32()
    {
        try
        {
            var resolved = PreferredSteamAccountResolver?.Invoke();
            if (!string.IsNullOrWhiteSpace(resolved))
                return resolved;
        }
        catch
        {
        }

        return PreferredSteamAccountId32;
    }

    public async Task<(string? dotaPath, string? steamPath)> FindDota2Async()
    {
        return await Task.Run(() =>
        {
            _steamPath = GetSteamPathFromRegistry();
            var steamPaths = GetAllSteamPaths();
            
            foreach (var steamPath in steamPaths)
            {
                var dotaPath = FindDotaInManifest(steamPath);
                if (dotaPath != null)
                {
                    _dotaPath = dotaPath;
                    return (dotaPath, _steamPath ?? steamPath);
                }
            }

            return ((string?)null, _steamPath);
        });
    }

    private List<string> GetAllSteamPaths()
    {
        var paths = new List<string>();

        try
        {
            using var steamKey = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            if (steamKey != null)
            {
                var mainPath = steamKey.GetValue("SteamPath") as string;
                if (mainPath != null)
                    paths.Add(mainPath.Replace("/", "\\"));
            }
        }
        catch { }

        var commonPaths = new[]
        {
            @"C:\Program Files (x86)\Steam",
            @"C:\Program Files\Steam",
            @"D:\Steam",
            @"E:\Steam"
        };

        foreach (var p in commonPaths)
        {
            if (Directory.Exists(p) && !paths.Contains(p))
                paths.Add(p);
        }

        try
        {
            foreach (var basePath in paths.ToList())
            {
                var libraryVdf = Path.Combine(basePath, "steamapps", "libraryfolders.vdf");
                if (File.Exists(libraryVdf))
                {
                    var content = File.ReadAllText(libraryVdf);
                    foreach (var line in content.Split('\n'))
                    {
                        if (line.Contains("path"))
                        {
                            var parts = line.Split('"');
                            if (parts.Length >= 4)
                            {
                                var libPath = parts[3].Trim().Replace("\\\\", "\\");
                                if (Directory.Exists(libPath) && !paths.Contains(libPath))
                                    paths.Add(libPath);
                            }
                        }
                    }
                }
            }
        }
        catch { }

        return paths;
    }

    private string? FindDotaInManifest(string steamPath)
    {
        var steamapps = Path.Combine(steamPath, "steamapps");
        if (!Directory.Exists(steamapps))
            return null;

        var manifestPath = Path.Combine(steamapps, "appmanifest_570.acf");
        if (File.Exists(manifestPath))
        {
            try
            {
                var content = File.ReadAllText(manifestPath);
                foreach (var line in content.Split('\n'))
                {
                    if (line.TrimStart().StartsWith("\"installdir\""))
                    {
                        var parts = line.Split('"');
                        if (parts.Length >= 4)
                        {
                            var installDir = parts[3];
                            var dotaPath = Path.Combine(steamapps, "common", installDir);
                            if (Directory.Exists(dotaPath))
                                return dotaPath;
                        }
                    }
                }
            }
            catch { }
        }

        var defaultBetaPath = Path.Combine(steamapps, "common", "dota 2 beta");
        if (Directory.Exists(defaultBetaPath))
            return defaultBetaPath;

        var defaultPath = Path.Combine(steamapps, "common", "dota 2");
        if (Directory.Exists(defaultPath))
            return defaultPath;

        return null;
    }

    public bool IsSteamRunning()
    {
        return System.Diagnostics.Process.GetProcessesByName("steam").Length > 0;
    }

    public async Task<bool> CloseSteamAsync()
    {
        return await Task.Run(() =>
        {
            if (!IsSteamRunning())
                return true;

            // Штатное завершение: Steam сохраняет localconfig.vdf только при чистом выходе.
            if (TryRequestSteamShutdown() && WaitForSteamToFullyExit(20000))
                return true;

            var processes = System.Diagnostics.Process.GetProcessesByName("steam");

            foreach (var process in processes)
            {
                try
                {
                    if (!process.HasExited)
                        process.CloseMainWindow();
                }
                catch { }
            }

            if (WaitForSteamToFullyExit(12000))
                return true;

            processes = System.Diagnostics.Process.GetProcessesByName("steam");
            foreach (var process in processes)
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill(true);
                }
                catch { }
            }

            return WaitForSteamToFullyExit(15000);
        });
    }

    private bool TryRequestSteamShutdown()
    {
        try
        {
            var steamPath = GetSteamPathFromRegistry();
            if (!string.IsNullOrWhiteSpace(steamPath))
            {
                var steamExePath = Path.Combine(steamPath, "steam.exe");
                if (File.Exists(steamExePath))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = steamExePath,
                        Arguments = "-shutdown",
                        WorkingDirectory = steamPath,
                        UseShellExecute = true
                    });
                    return true;
                }
            }

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "steam://exit",
                UseShellExecute = true
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> StartSteamAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                var steamPath = GetSteamPathFromRegistry();
                WaitForSteamToFullyExit();

                if (!string.IsNullOrWhiteSpace(steamPath))
                {
                    var steamExePath = Path.Combine(steamPath, "steam.exe");
                    if (File.Exists(steamExePath))
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = steamExePath,
                            WorkingDirectory = steamPath,
                            UseShellExecute = true
                        });

                        if (WaitForSteamToStart())
                            return true;
                    }
                }

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "steam://open/main",
                    UseShellExecute = true
                });

                return WaitForSteamToStart();
            }
            catch
            {
                return false;
            }
        });
    }

    private bool WaitForSteamToFullyExit(int timeoutMilliseconds = 10000)
    {
        var attempts = Math.Max(1, timeoutMilliseconds / 250);

        for (var i = 0; i < attempts; i++)
        {
            if (System.Diagnostics.Process.GetProcessesByName("steam").Length == 0)
                return true;

            System.Threading.Thread.Sleep(250);
        }

        return System.Diagnostics.Process.GetProcessesByName("steam").Length == 0;
    }

    private bool WaitForSteamToStart()
    {
        for (var i = 0; i < 40; i++)
        {
            if (System.Diagnostics.Process.GetProcessesByName("steam").Length > 0)
                return true;

            System.Threading.Thread.Sleep(250);
        }

        return false;
    }

    public async Task<string?> GetCurrentLaunchOptionsAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                var steamPath = GetSteamPathFromRegistry();
                if (string.IsNullOrWhiteSpace(steamPath))
                    return null;

                foreach (var configPath in GetLocalConfigPaths(steamPath))
                {
                    var existingOptions = GetExistingLaunchOptions(configPath);
                    if (!string.IsNullOrWhiteSpace(existingOptions))
                        return existingOptions;
                }
            }
            catch { }

            return null;
        });
    }

    public async Task<bool> HasLocalConfigAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                var steamPath = GetSteamPathFromRegistry();
                return !string.IsNullOrWhiteSpace(steamPath) && GetLocalConfigPaths(steamPath).Count > 0;
            }
            catch
            {
                return false;
            }
        });
    }

    public async Task<string?> GetPrimaryLocalConfigPathAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                var steamPath = GetSteamPathFromRegistry();
                if (string.IsNullOrWhiteSpace(steamPath))
                    return null;

                var configPaths = GetLocalConfigPaths(steamPath);
                return configPaths.Count == 0 ? null : configPaths[0];
            }
            catch
            {
                return null;
            }
        });
    }

    public async Task<string?> GetPrimaryVideoConfigPathAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                var userPath = GetPrimarySteamUserPath();
                if (string.IsNullOrWhiteSpace(userPath))
                    return null;

                return Path.Combine(userPath, "570", "local", "cfg", "video.txt");
            }
            catch
            {
                return null;
            }
        });
    }

    public async Task<string?> LoadVideoConfigAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                var videoPath = GetPrimarySteamUserPath();
                if (string.IsNullOrWhiteSpace(videoPath))
                    return null;

                var filePath = Path.Combine(videoPath, "570", "local", "cfg", "video.txt");
                var directory = Path.GetDirectoryName(filePath);
                if (directory != null)
                    Directory.CreateDirectory(directory);

                if (!File.Exists(filePath))
                {
                    File.WriteAllText(filePath, string.Empty);
                    return string.Empty;
                }

                return File.ReadAllText(filePath);
            }
            catch
            {
                return null;
            }
        });
    }

    public async Task<bool> SaveVideoConfigAsync(string content)
    {
        return await Task.Run(() =>
        {
            try
            {
                var userPath = GetPrimarySteamUserPath();
                if (string.IsNullOrWhiteSpace(userPath))
                    return false;

                var filePath = Path.Combine(userPath, "570", "local", "cfg", "video.txt");
                var directory = Path.GetDirectoryName(filePath);
                if (directory != null)
                    Directory.CreateDirectory(directory);

                if (File.Exists(filePath))
                {
                    var fileInfo = new FileInfo(filePath);
                    if (fileInfo.IsReadOnly)
                        fileInfo.IsReadOnly = false;
                }

                _fileBackupService?.BackupFile(filePath, "Dota 2 video.txt");
                File.WriteAllText(filePath, content);
                _logService?.Info($"Dota video config saved: {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                _logService?.Error("Failed to save Dota video config.", ex);
                return false;
            }
        });
    }

    public async Task<bool?> IsVideoConfigReadOnlyAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                var userPath = GetPrimarySteamUserPath();
                if (string.IsNullOrWhiteSpace(userPath))
                    return (bool?)null;

                var filePath = Path.Combine(userPath, "570", "local", "cfg", "video.txt");
                if (!File.Exists(filePath))
                    return false;

                return new FileInfo(filePath).IsReadOnly;
            }
            catch
            {
                return (bool?)null;
            }
        });
    }

    public async Task<bool> SetVideoConfigReadOnlyAsync(bool isReadOnly)
    {
        return await Task.Run(() =>
        {
            try
            {
                var userPath = GetPrimarySteamUserPath();
                if (string.IsNullOrWhiteSpace(userPath))
                    return false;

                var filePath = Path.Combine(userPath, "570", "local", "cfg", "video.txt");
                var directory = Path.GetDirectoryName(filePath);
                if (directory != null)
                    Directory.CreateDirectory(directory);

                if (!File.Exists(filePath))
                    File.WriteAllText(filePath, string.Empty);

                var fileInfo = new FileInfo(filePath)
                {
                    IsReadOnly = isReadOnly
                };

                return true;
            }
            catch
            {
                return false;
            }
        });
    }

    public async Task<bool> NeedsExactLaunchOptionsUpdateAsync(
        IEnumerable<string> selectedOptions,
        bool includeAutoexec)
    {
        return await Task.Run(() =>
        {
            try
            {
                var steamPath = GetSteamPathFromRegistry();
                if (string.IsNullOrWhiteSpace(steamPath))
                    return false;

                var configPaths = GetLocalConfigPaths(steamPath);
                if (configPaths.Count == 0)
                    return false;

                var selectedOptionList = NormalizeOptionList(selectedOptions);
                var desiredOptions = BuildExactLaunchOptions(selectedOptionList, includeAutoexec);

                foreach (var configPath in configPaths)
                {
                    var existingOptions = GetExistingLaunchOptions(configPath);
                    if (!LaunchOptionsEqual(existingOptions, desiredOptions))
                        return true;
                }
            }
            catch { }

            return false;
        });
    }

    public async Task<LaunchOptionsApplyResult> SetExactLaunchOptionsAsync(
        IEnumerable<string> selectedOptions,
        bool includeAutoexec)
    {
        return await Task.Run(() =>
        {
            try
            {
                var steamPath = GetSteamPathFromRegistry();
                if (string.IsNullOrWhiteSpace(steamPath))
                    return LaunchOptionsApplyResult.Failure("Steam не найден.");

                var configPaths = GetLocalConfigPaths(steamPath);
                if (configPaths.Count == 0)
                    return LaunchOptionsApplyResult.Failure("Не найден ни один localconfig.vdf.");

                var selectedOptionList = NormalizeOptionList(selectedOptions);
                var exactOptions = BuildExactLaunchOptions(selectedOptionList, includeAutoexec);
                var updatedFiles = 0;

                foreach (var configPath in configPaths)
                {
                    if (UpdateLocalConfig(configPath, exactOptions))
                        updatedFiles++;
                }

                if (updatedFiles == 0)
                    return LaunchOptionsApplyResult.Failure("Не удалось обновить LaunchOptions.");

                return LaunchOptionsApplyResult.Success(exactOptions, updatedFiles);
            }
            catch
            {
                return LaunchOptionsApplyResult.Failure("Ошибка при обновлении параметров запуска.");
            }
        });
    }

    public async Task<bool> NeedsLaunchOptionsUpdateAsync(
        IEnumerable<string> selectedOptions,
        IEnumerable<string> managedOptions,
        bool includeAutoexec)
    {
        return await Task.Run(() =>
        {
            try
            {
                var steamPath = GetSteamPathFromRegistry();
                if (string.IsNullOrWhiteSpace(steamPath))
                    return false;

                var configPaths = GetLocalConfigPaths(steamPath);
                if (configPaths.Count == 0)
                    return false;

                var selectedOptionList = NormalizeOptionList(selectedOptions);
                var managedOptionList = NormalizeOptionList(managedOptions);

                foreach (var configPath in configPaths)
                {
                    var existingOptions = GetExistingLaunchOptions(configPath);
                    var desiredOptions = BuildLaunchOptions(
                        selectedOptionList,
                        existingOptions,
                        managedOptionList,
                        includeAutoexec);

                    if (!LaunchOptionsEqual(existingOptions, desiredOptions))
                        return true;
                }
            }
            catch { }

            return false;
        });
    }

    public async Task<LaunchOptionsApplyResult> SetLaunchOptionsAsync(
        IEnumerable<string> selectedOptions,
        IEnumerable<string> managedOptions,
        bool includeAutoexec)
    {
        return await Task.Run(() =>
        {
            try
            {
                var steamPath = GetSteamPathFromRegistry();
                if (string.IsNullOrWhiteSpace(steamPath))
                    return LaunchOptionsApplyResult.Failure("Steam не найден.");

                var configPaths = GetLocalConfigPaths(steamPath);
                if (configPaths.Count == 0)
                    return LaunchOptionsApplyResult.Failure("Не найден ни один localconfig.vdf.");

                var selectedOptionList = NormalizeOptionList(selectedOptions);
                var managedOptionList = NormalizeOptionList(managedOptions);
                var updatedFiles = 0;
                string appliedOptions = string.Empty;

                foreach (var configPath in configPaths)
                {
                    var existingOptions = GetExistingLaunchOptions(configPath);
                    var optionsToApply = BuildLaunchOptions(
                        selectedOptionList,
                        existingOptions,
                        managedOptionList,
                        includeAutoexec);

                    if (UpdateLocalConfig(configPath, optionsToApply))
                    {
                        updatedFiles++;
                        appliedOptions = optionsToApply;
                    }
                }

                if (updatedFiles == 0)
                    return LaunchOptionsApplyResult.Failure("Не удалось обновить LaunchOptions.");

                return LaunchOptionsApplyResult.Success(appliedOptions, updatedFiles);
            }
            catch
            {
                return LaunchOptionsApplyResult.Failure("Ошибка при обновлении параметров запуска.");
            }
        });
    }

    // Путь до блока с параметрами запуска внутри localconfig.vdf. В файле есть ещё
    // минимум две секции "apps" (CDN-токены и настройки контроллера) со своими блоками
    // "570" — писать в них нельзя, поэтому секция определяется по полному пути.
    private static readonly string[] SteamAppsPath = { "software", "valve", "steam", "apps" };
    private static readonly string[] DotaAppPath = { "software", "valve", "steam", "apps", "570" };

    private string? GetExistingLaunchOptions(string configPath)
    {
        try
        {
            var content = File.ReadAllText(configPath);
            var lines = content.Split('\n');
            var stack = new List<string>();
            string? pendingKey = null;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                if (trimmed == "{")
                {
                    stack.Add(pendingKey ?? string.Empty);
                    pendingKey = null;
                    continue;
                }

                if (trimmed == "}")
                {
                    if (stack.Count > 0)
                        stack.RemoveAt(stack.Count - 1);
                    pendingKey = null;
                    continue;
                }

                if (TryGetBareKey(trimmed, out var key))
                {
                    pendingKey = key;
                    continue;
                }

                pendingKey = null;

                if (StackEndsWith(stack, DotaAppPath) && line.Contains("\"LaunchOptions\""))
                    return ExtractQuotedValue(line, "LaunchOptions");
            }
        }
        catch { }
        return null;
    }

    private static bool TryGetBareKey(string trimmedLine, out string key)
    {
        key = string.Empty;

        if (trimmedLine.Length < 2
            || trimmedLine[0] != '"'
            || trimmedLine[^1] != '"'
            || trimmedLine.IndexOf('"', 1) != trimmedLine.Length - 1)
        {
            return false;
        }

        key = trimmedLine[1..^1];
        return true;
    }

    private static bool StackEndsWith(List<string> stack, string[] suffix)
    {
        if (stack.Count < suffix.Length)
            return false;

        for (var i = 0; i < suffix.Length; i++)
        {
            if (!string.Equals(stack[stack.Count - suffix.Length + i], suffix[i], StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    private string? GetSteamPathFromRegistry()
    {
        try
        {
            using var steamKey = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            if (steamKey != null)
            {
                var path = steamKey.GetValue("SteamPath") as string;
                if (path != null)
                    return path.Replace("/", "\\");
            }
        }
        catch { }

        var commonPaths = new[]
        {
            @"C:\Program Files (x86)\Steam",
            @"C:\Program Files\Steam",
            @"D:\Steam",
            @"E:\Steam"
        };

        foreach (var path in commonPaths)
        {
            if (Directory.Exists(path))
                return path;
        }

        return null;
    }

    private List<string> GetLocalConfigPaths(string steamPath)
    {
        return SteamUserResolver.GetTargetLocalConfigPaths(steamPath, ResolvePreferredSteamAccountId32());
    }

    private string? GetPrimarySteamUserPath()
    {
        var steamPath = GetSteamPathFromRegistry();
        if (string.IsNullOrWhiteSpace(steamPath))
            return null;

        return SteamUserResolver.GetPrimarySteamUserPath(steamPath, ResolvePreferredSteamAccountId32());
    }

    public async Task<IReadOnlyList<SteamUserInfo>> GetSteamUsersAsync()
    {
        return await Task.Run(() =>
        {
            var steamPath = GetSteamPathFromRegistry();
            return string.IsNullOrWhiteSpace(steamPath)
                ? Array.Empty<SteamUserInfo>()
                : SteamUserResolver.GetSteamUsers(steamPath).ToArray();
        });
    }

    private static List<string> NormalizeOptionList(IEnumerable<string> options)
    {
        var result = new List<string>();

        foreach (var option in options)
        {
            if (string.IsNullOrWhiteSpace(option))
                continue;

            var trimmedOption = option.Trim();
            if (!result.Contains(trimmedOption, StringComparer.OrdinalIgnoreCase))
                result.Add(trimmedOption);
        }

        return result;
    }

    private static string BuildLaunchOptions(
        IReadOnlyList<string> selectedOptions,
        string? existingOptions,
        IReadOnlyList<string> managedOptions,
        bool includeAutoexec)
    {
        var preservedOptions = RemoveExecPrefix(existingOptions ?? string.Empty);
        preservedOptions = RemoveManagedOptions(preservedOptions, managedOptions);

        var finalOptions = new List<string>();

        if (includeAutoexec)
            finalOptions.Add(AutoexecLaunchCommand);

        foreach (var option in selectedOptions)
            finalOptions.Add(option);

        if (!string.IsNullOrWhiteSpace(preservedOptions))
            finalOptions.Add(preservedOptions);

        return string.Join(" ", finalOptions).Trim();
    }

    private static string BuildExactLaunchOptions(
        IReadOnlyList<string> selectedOptions,
        bool includeAutoexec)
    {
        var finalOptions = new List<string>();

        if (includeAutoexec)
            finalOptions.Add(AutoexecLaunchCommand);

        foreach (var option in selectedOptions)
            finalOptions.Add(option);

        return string.Join(" ", finalOptions).Trim();
    }

    private static string RemoveManagedOptions(string options, IReadOnlyList<string> managedOptions)
    {
        var cleaned = options;

        foreach (var managedOption in managedOptions)
        {
            cleaned = Regex.Replace(
                cleaned,
                $@"(?<!\S){Regex.Escape(managedOption)}(?!\S)",
                string.Empty,
                RegexOptions.IgnoreCase);
        }

        return Regex.Replace(cleaned, @"\s+", " ").Trim();
    }

    private static string RemoveExecPrefix(string options)
    {
        var cleaned = Regex.Replace(options, @"\+exec\s+autoexec\.cfg(?:\.txt)?", string.Empty, RegexOptions.IgnoreCase);
        return Regex.Replace(cleaned, @"\s+", " ").Trim();
    }

    private static bool LaunchOptionsEqual(string? left, string? right)
    {
        var normalizedLeft = Regex.Replace(left ?? string.Empty, @"\s+", " ").Trim();
        var normalizedRight = Regex.Replace(right ?? string.Empty, @"\s+", " ").Trim();
        return string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
    }

    private bool UpdateLocalConfig(string configPath, string options)
    {
        try
        {
            var content = File.ReadAllText(configPath);

            if (!content.Contains("\"apps\""))
                return false;

            var lineEndingSuffix = content.Contains("\r\n") ? "\r" : string.Empty;
            var lines = content.Split('\n');
            var result = new List<string>(lines.Length + 4);
            var stack = new List<string>();
            string? pendingKey = null;
            var updated = false;
            var dotaBlockSeenInTargetApps = false;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                if (trimmed == "{")
                {
                    stack.Add(pendingKey ?? string.Empty);
                    pendingKey = null;
                    result.Add(line);
                    continue;
                }

                if (trimmed == "}")
                {
                    if (!updated && StackEndsWith(stack, DotaAppPath))
                    {
                        result.Add(CreateQuotedValueLine(line, "LaunchOptions", options) + lineEndingSuffix);
                        updated = true;
                    }
                    else if (!updated && !dotaBlockSeenInTargetApps && StackEndsWith(stack, SteamAppsPath))
                    {
                        var indentation = GetIndentation(line) + "\t";
                        result.Add($"{indentation}\"570\"{lineEndingSuffix}");
                        result.Add($"{indentation}{{{lineEndingSuffix}");
                        result.Add($"{indentation}\t\"LaunchOptions\"\t\t\"{EscapeVdfValue(options)}\"{lineEndingSuffix}");
                        result.Add($"{indentation}}}{lineEndingSuffix}");
                        updated = true;
                    }

                    if (stack.Count > 0)
                        stack.RemoveAt(stack.Count - 1);
                    pendingKey = null;
                    result.Add(line);
                    continue;
                }

                if (TryGetBareKey(trimmed, out var key))
                {
                    pendingKey = key;
                    if (key == "570" && StackEndsWith(stack, SteamAppsPath))
                        dotaBlockSeenInTargetApps = true;

                    result.Add(line);
                    continue;
                }

                pendingKey = null;

                if (!updated && StackEndsWith(stack, DotaAppPath) && line.Contains("\"LaunchOptions\""))
                {
                    result.Add(ReplaceQuotedValue(line, "LaunchOptions", options));
                    updated = true;
                    continue;
                }

                result.Add(line);
            }

            if (!updated)
                return false;

            _fileBackupService?.BackupFile(configPath, "Dota 2 localconfig.vdf");
            File.WriteAllText(configPath, string.Join("\n", result));
            _logService?.Info($"Dota launch options updated: {configPath}");
            return true;
        }
        catch (Exception ex)
        {
            _logService?.Error($"Failed to update Dota localconfig: {configPath}", ex);
            return false;
        }
    }

    private static string? ExtractQuotedValue(string line, string key)
    {
        var match = Regex.Match(line, $"\\\"{Regex.Escape(key)}\\\"\\s*\\\"(?<value>.*)\\\"");
        return match.Success ? UnescapeVdfValue(match.Groups["value"].Value).Trim() : null;
    }

    private static string ReplaceQuotedValue(string line, string key, string value)
    {
        return Regex.Replace(
            line,
            $"(?<prefix>\\\"{Regex.Escape(key)}\\\"\\s*\\\").*(?<suffix>\\\")",
            match => match.Groups["prefix"].Value + EscapeVdfValue(value) + match.Groups["suffix"].Value);
    }

    private static string CreateQuotedValueLine(string closingBraceLine, string key, string value)
    {
        var indentation = GetIndentation(closingBraceLine);
        var childIndentation = indentation.Contains('\t') ? indentation + "\t" : indentation + "    ";
        return $"{childIndentation}\"{key}\"\t\t\"{EscapeVdfValue(value)}\"";
    }

    private static string GetIndentation(string line)
    {
        var count = 0;
        while (count < line.Length && char.IsWhiteSpace(line[count]))
            count++;

        return line[..count];
    }

    private static string EscapeVdfValue(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static string UnescapeVdfValue(string value)
    {
        var result = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '\\' && i + 1 < value.Length)
            {
                result.Append(value[i + 1]);
                i++;
                continue;
            }

            result.Append(value[i]);
        }

        return result.ToString();
    }

    public sealed class LaunchOptionsApplyResult
    {
        public bool IsSuccess { get; init; }
        public string Message { get; init; } = string.Empty;
        public string AppliedOptions { get; init; } = string.Empty;
        public int UpdatedFiles { get; init; }

        public static LaunchOptionsApplyResult Success(string appliedOptions, int updatedFiles)
        {
            return new LaunchOptionsApplyResult
            {
                IsSuccess = true,
                AppliedOptions = appliedOptions,
                UpdatedFiles = updatedFiles,
                Message = string.IsNullOrWhiteSpace(appliedOptions)
                    ? "Параметры запуска очищены."
                    : updatedFiles == 1
                        ? "Параметры запуска обновлены."
                        : $"Параметры запуска обновлены в {updatedFiles} профилях Steam."
            };
        }

        public static LaunchOptionsApplyResult Failure(string message)
        {
            return new LaunchOptionsApplyResult
            {
                IsSuccess = false,
                Message = message
            };
        }
    }

    public async Task SaveAutoexecAsync(string content)
    {
        if (_dotaPath == null)
            return;

        await Task.Run(() =>
        {
            try
            {
                var cfgPath = GetAutoexecPath(AutoexecFileName);
                var dir = Path.GetDirectoryName(cfgPath);
                if (dir != null)
                    Directory.CreateDirectory(dir);
                _fileBackupService?.BackupFile(cfgPath, "Dota 2 autoexec.cfg");
                File.WriteAllText(cfgPath, content);
                _logService?.Info($"Dota autoexec saved: {cfgPath}");
            }
            catch (Exception ex)
            {
                _logService?.Error("Failed to save Dota autoexec.", ex);
            }
        });
    }

    public async Task<string?> LoadAutoexecAsync()
    {
        if (_dotaPath == null)
            return null;

        return await Task.Run(() =>
        {
            try
            {
                var cfgPath = GetAutoexecPath(AutoexecFileName);
                var dir = Path.GetDirectoryName(cfgPath);
                if (dir != null)
                    Directory.CreateDirectory(dir);

                var legacyCfgPath = GetAutoexecPath(LegacyAutoexecFileName);
                if (!File.Exists(cfgPath) && File.Exists(legacyCfgPath))
                {
                    var legacyContent = File.ReadAllText(legacyCfgPath);
                    File.WriteAllText(cfgPath, legacyContent);
                    return legacyContent;
                }

                if (!File.Exists(cfgPath))
                {
                    File.WriteAllText(cfgPath, string.Empty);
                    return string.Empty;
                }

                return File.ReadAllText(cfgPath);
            }
            catch { }

            return null;
        });
    }

    private string GetAutoexecPath(string fileName)
    {
        return Path.Combine(_dotaPath!, "game", "dota", "cfg", fileName);
    }
}
