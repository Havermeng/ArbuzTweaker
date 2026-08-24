using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace ArbuzTweaker;

public sealed class ScpSlService
{
    private const string AppId = "700330";

    private readonly FileBackupService? _fileBackupService;
    private readonly AppLogService? _logService;
    private string? _gamePath;
    private string? _steamPath;

    public ScpSlService()
    {
    }

    public ScpSlService(FileBackupService fileBackupService, AppLogService logService)
    {
        _fileBackupService = fileBackupService;
        _logService = logService;
    }

    public string? GamePath => _gamePath;

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

    public async Task<(string? gamePath, string? steamPath)> FindGameAsync()
    {
        return await Task.Run(() =>
        {
            _steamPath = GetSteamPathFromRegistry();
            var steamPaths = GetAllSteamPaths();

            foreach (var steamPath in steamPaths)
            {
                var gamePath = FindGameInManifest(steamPath);
                if (gamePath != null)
                {
                    _gamePath = gamePath;
                    return (gamePath, _steamPath ?? steamPath);
                }
            }

            return ((string?)null, _steamPath);
        });
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
                catch
                {
                }
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
                catch
                {
                }
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
            catch
            {
            }

            return null;
        });
    }

    public async Task<bool> NeedsExactLaunchOptionsUpdateAsync(IEnumerable<string> selectedOptions)
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

                var exactOptions = BuildExactLaunchOptions(NormalizeOptionList(selectedOptions));

                foreach (var configPath in configPaths)
                {
                    var existingOptions = GetExistingLaunchOptions(configPath);
                    if (!LaunchOptionsEqual(existingOptions, exactOptions))
                        return true;
                }
            }
            catch
            {
            }

            return false;
        });
    }

    public async Task<ScpLaunchOptionsApplyResult> SetExactLaunchOptionsAsync(IEnumerable<string> selectedOptions)
    {
        return await Task.Run(() =>
        {
            try
            {
                var steamPath = GetSteamPathFromRegistry();
                if (string.IsNullOrWhiteSpace(steamPath))
                    return ScpLaunchOptionsApplyResult.Failure("Steam не найден.");

                var configPaths = GetLocalConfigPaths(steamPath);
                if (configPaths.Count == 0)
                    return ScpLaunchOptionsApplyResult.Failure("Не найден ни один localconfig.vdf.");

                var exactOptions = BuildExactLaunchOptions(NormalizeOptionList(selectedOptions));
                var updatedFiles = 0;

                foreach (var configPath in configPaths)
                {
                    if (UpdateLocalConfig(configPath, exactOptions))
                        updatedFiles++;
                }

                if (updatedFiles == 0)
                    return ScpLaunchOptionsApplyResult.Failure("Не удалось обновить LaunchOptions для SCP:SL.");

                return ScpLaunchOptionsApplyResult.Success(exactOptions, updatedFiles);
            }
            catch (Exception ex)
            {
                _logService?.Error("Failed to update SCP:SL launch options.", ex);
                return ScpLaunchOptionsApplyResult.Failure("Ошибка при обновлении параметров запуска.");
            }
        });
    }

    public string GetCommandBindingsPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SCP Secret Laboratory",
            "cmdbinding.txt");
    }

    public async Task<string?> LoadCommandBindingsAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                var path = GetCommandBindingsPath();
                return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
            }
            catch (Exception ex)
            {
                _logService?.Error("Failed to load SCP:SL command bindings.", ex);
                return null;
            }
        });
    }

    public async Task<bool> SaveCommandBindingsAsync(string content)
    {
        return await Task.Run(() =>
        {
            try
            {
                var path = GetCommandBindingsPath();
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                _fileBackupService?.BackupFile(path, "SCP SL cmdbinding.txt");
                File.WriteAllText(path, content);
                _logService?.Info($"SCP:SL command bindings saved: {path}");
                return true;
            }
            catch (Exception ex)
            {
                _logService?.Error("Failed to save SCP:SL command bindings.", ex);
                return false;
            }
        });
    }

    // boot.config — текстовый конфиг движка Unity в папке установки игры
    // (…\SCP Secret Laboratory\SCPSL_Data\boot.config). Формат — строки key=value.
    public async Task<string?> GetBootConfigPathAsync()
    {
        var gamePath = _gamePath;
        if (string.IsNullOrWhiteSpace(gamePath))
        {
            var (foundPath, _) = await FindGameAsync();
            gamePath = foundPath;
        }

        if (string.IsNullOrWhiteSpace(gamePath))
            return null;

        return Path.Combine(gamePath, "SCPSL_Data", "boot.config");
    }

    public async Task<string?> LoadBootConfigAsync()
    {
        try
        {
            var path = await GetBootConfigPathAsync();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;

            return await File.ReadAllTextAsync(path);
        }
        catch (Exception ex)
        {
            _logService?.Error("Failed to load SCP:SL boot.config.", ex);
            return null;
        }
    }

    public async Task<bool> SaveBootConfigAsync(string content)
    {
        try
        {
            var path = await GetBootConfigPathAsync();
            if (string.IsNullOrWhiteSpace(path))
                return false;

            var directory = Path.GetDirectoryName(path);
            // Папку SCPSL_Data сами не создаём: если её нет — игра не найдена, писать некуда.
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                return false;

            _fileBackupService?.BackupFile(path, "SCP SL boot.config");
            await File.WriteAllTextAsync(path, content);
            _logService?.Info($"SCP:SL boot.config saved: {path}");
            return true;
        }
        catch (Exception ex)
        {
            _logService?.Error("Failed to save SCP:SL boot.config.", ex);
            return false;
        }
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
        catch
        {
        }

        var commonPaths = new[]
        {
            @"C:\Program Files (x86)\Steam",
            @"C:\Program Files\Steam",
            @"D:\Steam",
            @"E:\Steam"
        };

        foreach (var path in commonPaths)
        {
            if (Directory.Exists(path) && !paths.Contains(path))
                paths.Add(path);
        }

        try
        {
            foreach (var basePath in paths.ToList())
            {
                var libraryVdf = Path.Combine(basePath, "steamapps", "libraryfolders.vdf");
                if (!File.Exists(libraryVdf))
                    continue;

                var content = File.ReadAllText(libraryVdf);
                foreach (var line in content.Split('\n'))
                {
                    if (!line.Contains("path"))
                        continue;

                    var parts = line.Split('"');
                    if (parts.Length < 4)
                        continue;

                    var libraryPath = parts[3].Trim().Replace("\\\\", "\\");
                    if (Directory.Exists(libraryPath) && !paths.Contains(libraryPath))
                        paths.Add(libraryPath);
                }
            }
        }
        catch
        {
        }

        return paths;
    }

    private string? FindGameInManifest(string steamPath)
    {
        var steamapps = Path.Combine(steamPath, "steamapps");
        if (!Directory.Exists(steamapps))
            return null;

        var manifestPath = Path.Combine(steamapps, $"appmanifest_{AppId}.acf");
        if (File.Exists(manifestPath))
        {
            try
            {
                var content = File.ReadAllText(manifestPath);
                foreach (var line in content.Split('\n'))
                {
                    if (!line.TrimStart().StartsWith("\"installdir\""))
                        continue;

                    var parts = line.Split('"');
                    if (parts.Length >= 4)
                    {
                        var installDir = parts[3];
                        var gamePath = Path.Combine(steamapps, "common", installDir);
                        if (Directory.Exists(gamePath))
                            return gamePath;
                    }
                }
            }
            catch
            {
            }
        }

        var defaultPath = Path.Combine(steamapps, "common", "SCP Secret Laboratory");
        return Directory.Exists(defaultPath) ? defaultPath : null;
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
        catch
        {
        }

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

    // Путь до блока с параметрами запуска внутри localconfig.vdf. В файле есть ещё
    // минимум две секции "apps" (CDN-токены и настройки контроллера) со своими блоками
    // приложений — писать в них нельзя, поэтому секция определяется по полному пути.
    private static readonly string[] SteamAppsPath = { "software", "valve", "steam", "apps" };
    private static readonly string[] GameAppPath = { "software", "valve", "steam", "apps", AppId };

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

                if (StackEndsWith(stack, GameAppPath) && line.Contains("\"LaunchOptions\""))
                    return ExtractQuotedValue(line, "LaunchOptions");
            }
        }
        catch
        {
        }

        return null;
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
            var gameBlockSeenInTargetApps = false;

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
                    if (!updated && StackEndsWith(stack, GameAppPath))
                    {
                        result.Add(CreateQuotedValueLine(line, "LaunchOptions", options) + lineEndingSuffix);
                        updated = true;
                    }
                    else if (!updated && !gameBlockSeenInTargetApps && StackEndsWith(stack, SteamAppsPath))
                    {
                        var indentation = GetIndentation(line) + "\t";
                        result.Add($"{indentation}\"{AppId}\"{lineEndingSuffix}");
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
                    if (key == AppId && StackEndsWith(stack, SteamAppsPath))
                        gameBlockSeenInTargetApps = true;

                    result.Add(line);
                    continue;
                }

                pendingKey = null;

                if (!updated && StackEndsWith(stack, GameAppPath) && line.Contains("\"LaunchOptions\""))
                {
                    result.Add(ReplaceQuotedValue(line, "LaunchOptions", options));
                    updated = true;
                    continue;
                }

                result.Add(line);
            }

            if (!updated)
                return false;

            _fileBackupService?.BackupFile(configPath, "SCP SL localconfig.vdf");
            File.WriteAllText(configPath, string.Join("\n", result));
            _logService?.Info($"SCP:SL launch options updated: {configPath}");
            return true;
        }
        catch (Exception ex)
        {
            _logService?.Error($"Failed to update SCP:SL localconfig: {configPath}", ex);
            return false;
        }
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

    private static string BuildExactLaunchOptions(IReadOnlyList<string> selectedOptions)
    {
        return string.Join(" ", selectedOptions).Trim();
    }

    private static bool LaunchOptionsEqual(string? left, string? right)
    {
        var normalizedLeft = Regex.Replace(left ?? string.Empty, @"\s+", " ").Trim();
        var normalizedRight = Regex.Replace(right ?? string.Empty, @"\s+", " ").Trim();
        return string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
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
}

public sealed class ScpLaunchOptionsApplyResult
{
    public bool IsSuccess { get; init; }

    public string Message { get; init; } = string.Empty;

    public string AppliedOptions { get; init; } = string.Empty;

    public int UpdatedFiles { get; init; }

    public static ScpLaunchOptionsApplyResult Success(string appliedOptions, int updatedFiles)
    {
        return new ScpLaunchOptionsApplyResult
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

    public static ScpLaunchOptionsApplyResult Failure(string message)
    {
        return new ScpLaunchOptionsApplyResult
        {
            IsSuccess = false,
            Message = message
        };
    }
}
