using System;
using System.IO;

namespace ArbuzTweaker;

public sealed class AppSettingsService
{
    private const string SettingsFileName = "settings.json";
    private readonly ConfigService _configService;

    public AppSettingsService(ConfigService configService)
    {
        _configService = configService;
    }

    public AppSettingsData Load()
    {
        try
        {
            var path = GetSettingsPath();
            if (!File.Exists(path))
                return new AppSettingsData();

            var content = File.ReadAllText(path);
            var settings = System.Text.Json.JsonSerializer.Deserialize<AppSettingsData>(content) ?? new AppSettingsData();
            if (!settings.UnsafeTweaksRiskAccepted)
                settings.SafeModeUserConfigOnly = true;

            return settings;
        }
        catch
        {
            return new AppSettingsData();
        }
    }

    public void Save(AppSettingsData settings)
    {
        try
        {
            var path = GetSettingsPath();
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var content = System.Text.Json.JsonSerializer.Serialize(
                settings,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

            var tempPath = path + ".tmp";
            File.WriteAllText(tempPath, content);
            File.Move(tempPath, path, true);
        }
        catch
        {
        }
    }

    private string GetSettingsPath()
    {
        return Path.Combine(_configService.ConfigsPath, SettingsFileName);
    }
}

public sealed class AppSettingsData
{
    public bool CheckForUpdatesOnStartup { get; set; } = true;
    public int WindowLeft { get; set; }
    public int WindowTop { get; set; }
    public int WindowWidth { get; set; }
    public int WindowHeight { get; set; }
    public bool WindowMaximized { get; set; }
    public bool SafeModeUserConfigOnly { get; set; } = true;
    public bool UnsafeTweaksRiskAccepted { get; set; }
    public bool NvidiaOverlayPreLaunchDota2 { get; set; }
    public bool NvidiaOverlayPreLaunchScpSl { get; set; }
    public bool NvidiaOverlayPreLaunchCustomProgram { get; set; }
    public string NvidiaOverlayPreLaunchCustomProgramPath { get; set; } = string.Empty;
    public string PreferredSteamAccountId32 { get; set; } = string.Empty;
}
