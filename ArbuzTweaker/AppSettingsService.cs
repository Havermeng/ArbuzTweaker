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
            return System.Text.Json.JsonSerializer.Deserialize<AppSettingsData>(content) ?? new AppSettingsData();
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
            var content = System.Text.Json.JsonSerializer.Serialize(
                settings,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

            File.WriteAllText(GetSettingsPath(), content);
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
}
