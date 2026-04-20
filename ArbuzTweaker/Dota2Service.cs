using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace ArbuzTweaker;

public class Dota2Service
{
    public const string AutoexecFileName = "autoexec.cfg.txt";
    public const string AutoexecLaunchCommand = "+exec autoexec.cfg.txt";

    private string? _dotaPath;
    private string? _steamPath;

    public string? DotaPath => _dotaPath;
    public string? SteamPath => _steamPath;

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

    public bool StartSteam()
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

                File.WriteAllText(filePath, content);
                return true;
            }
            catch
            {
                return false;
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

    private string? GetExistingLaunchOptions(string configPath)
    {
        try
        {
            var content = File.ReadAllText(configPath);
            var lines = content.Split('\n');
            bool inDotaSection = false;
            int braceCount = 0;

            foreach (var line in lines)
            {
                if (line.Trim().StartsWith("\"570\""))
                {
                    inDotaSection = true;
                    braceCount = 0;
                }

                if (inDotaSection)
                {
                    braceCount += line.Count(c => c == '{') - line.Count(c => c == '}');

                    if (line.Contains("\"LaunchOptions\""))
                    {
                        return ExtractQuotedValue(line, "LaunchOptions");
                    }

                    if (braceCount == 0 && line.Trim() == "}")
                    {
                        break;
                    }
                }
            }
        }
        catch { }
        return null;
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
        var result = new List<string>();
        var userDataPath = Path.Combine(steamPath, "userdata");

        if (!Directory.Exists(userDataPath))
            return result;

        foreach (var userPath in Directory.GetDirectories(userDataPath))
        {
            var configPath = Path.Combine(userPath, "config", "localconfig.vdf");
            if (File.Exists(configPath))
                result.Add(configPath);
        }

        return result;
    }

    private string? GetPrimarySteamUserPath()
    {
        var steamPath = GetSteamPathFromRegistry();
        if (string.IsNullOrWhiteSpace(steamPath))
            return null;

        var configPaths = GetLocalConfigPaths(steamPath);
        if (configPaths.Count > 0)
        {
            var configDirectory = Path.GetDirectoryName(configPaths[0]);
            var userDirectory = configDirectory == null ? null : Directory.GetParent(configDirectory);
            if (userDirectory != null)
                return userDirectory.FullName;
        }

        var userDataPath = Path.Combine(steamPath, "userdata");
        if (!Directory.Exists(userDataPath))
            return null;

        var userDirectories = Directory.GetDirectories(userDataPath);
        return userDirectories.Length == 0 ? null : userDirectories[0];
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
            
            if (content.Contains("\"570\""))
            {
                var lines = content.Split('\n');
                var result = new List<string>();
                bool inDotaSection = false;
                bool updated = false;
                int braceCount = 0;

                foreach (var line in lines)
                {
                    string newLine = line;
                    
                    if (line.Trim().StartsWith("\"570\""))
                    {
                        inDotaSection = true;
                        braceCount = 0;
                    }
                    
                    if (inDotaSection)
                    {
                        braceCount += line.Count(c => c == '{') - line.Count(c => c == '}');
                        
                        if (line.Contains("\"LaunchOptions\""))
                        {
                            newLine = ReplaceQuotedValue(line, "LaunchOptions", options);
                            updated = true;
                        }

                        if (braceCount == 0 && line.Trim() == "}" && !updated)
                        {
                            inDotaSection = false;
                        }
                        else if (braceCount == 0 && line.Trim() == "}")
                        {
                            inDotaSection = false;
                        }
                    }

                    result.Add(newLine);
                }

                if (!updated)
                    return false;

                File.WriteAllText(configPath, string.Join("\n", result));
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static string? ExtractQuotedValue(string line, string key)
    {
        var match = Regex.Match(line, $"\\\"{Regex.Escape(key)}\\\"\\s*\\\"(?<value>.*)\\\"");
        return match.Success ? match.Groups["value"].Value.Trim() : null;
    }

    private static string ReplaceQuotedValue(string line, string key, string value)
    {
        return Regex.Replace(
            line,
            $"(\\\"{Regex.Escape(key)}\\\"\\s*\\\").*(\\\")",
            $"$1{value}$2");
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
                var cfgPath = Path.Combine(_dotaPath, "game", "dota", "cfg", AutoexecFileName);
                var dir = Path.GetDirectoryName(cfgPath);
                if (dir != null)
                    Directory.CreateDirectory(dir);
                File.WriteAllText(cfgPath, content);
            }
            catch { }
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
                var cfgPath = Path.Combine(_dotaPath, "game", "dota", "cfg", AutoexecFileName);
                var dir = Path.GetDirectoryName(cfgPath);
                if (dir != null)
                    Directory.CreateDirectory(dir);

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
}
