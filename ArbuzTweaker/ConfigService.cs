using System;
using System.IO;
using System.Threading.Tasks;

namespace ArbuzTweaker;

public class ConfigService
{
    private readonly string _appDataPath;
    private readonly string _configsPath;

    public ConfigService()
    {
        _appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ArbuzTweaker");
        _configsPath = Path.Combine(_appDataPath, "Configs");
    }

    public string AppDataPath => _appDataPath;
    public string ConfigsPath => _configsPath;

    public void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(_appDataPath);
        Directory.CreateDirectory(_configsPath);
    }

    public async Task<string> LoadConfigAsync(string configName)
    {
        try
        {
            Directory.CreateDirectory(_configsPath);
            var path = Path.Combine(_configsPath, configName);
            if (File.Exists(path))
                return await File.ReadAllTextAsync(path);
        }
        catch
        {
        }

        return string.Empty;
    }

    public async Task SaveConfigAsync(string configName, string content)
    {
        try
        {
            Directory.CreateDirectory(_configsPath);
            var path = Path.Combine(_configsPath, configName);
            await File.WriteAllTextAsync(path, content);
        }
        catch
        {
        }
    }

    public bool ConfigExists(string configName)
    {
        return File.Exists(Path.Combine(_configsPath, configName));
    }
}
