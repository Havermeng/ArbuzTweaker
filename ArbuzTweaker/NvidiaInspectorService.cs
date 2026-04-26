using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace ArbuzTweaker;

public sealed class NvidiaInspectorService
{
    private const string Owner = "Orbmu2k";
    private const string Repo = "nvidiaProfileInspector";
    private const string AssetName = "nvidiaProfileInspector.zip";
    private const string VersionFileName = ".version";

    private readonly string _installDirectory;

    public NvidiaInspectorService(ConfigService configService)
    {
        _installDirectory = Path.Combine(configService.AppDataPath, "Tools", "NVIDIA Profile Inspector");
    }

    public string InstallDirectory => _installDirectory;

    public string ExecutablePath => Path.Combine(_installDirectory, "nvidiaProfileInspector.exe");

    public bool IsInstalled => File.Exists(ExecutablePath);

    public string InstalledVersion
    {
        get
        {
            try
            {
                var versionPath = Path.Combine(_installDirectory, VersionFileName);
                return File.Exists(versionPath) ? File.ReadAllText(versionPath).Trim() : "неизвестно";
            }
            catch
            {
                return "неизвестно";
            }
        }
    }

    public async Task<ThirdPartyToolInstallResult> InstallLatestAsync()
    {
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ArbuzTweaker");

            var metadataJson = await client.GetStringAsync($"https://api.github.com/repos/{Owner}/{Repo}/releases/latest");
            using var document = JsonDocument.Parse(metadataJson);

            var root = document.RootElement;
            var tagName = root.GetProperty("tag_name").GetString() ?? "unknown";

            string? downloadUrl = null;
            foreach (var asset in root.GetProperty("assets").EnumerateArray())
            {
                if (string.Equals(asset.GetProperty("name").GetString(), AssetName, StringComparison.OrdinalIgnoreCase))
                {
                    downloadUrl = asset.GetProperty("browser_download_url").GetString();
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(downloadUrl))
                return ThirdPartyToolInstallResult.Failure("Не удалось найти архив NVIDIA Inspector в последнем релизе.");

            var tempRoot = Path.Combine(Path.GetTempPath(), "ArbuzTweaker-NvidiaInspector");
            var zipPath = Path.Combine(tempRoot, AssetName);
            var extractPath = Path.Combine(tempRoot, "extracted");

            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, true);

            Directory.CreateDirectory(tempRoot);
            Directory.CreateDirectory(extractPath);

            await using (var zipStream = await client.GetStreamAsync(downloadUrl))
            await using (var fileStream = File.Create(zipPath))
            {
                await zipStream.CopyToAsync(fileStream);
            }

            ZipFile.ExtractToDirectory(zipPath, extractPath, true);

            if (Directory.Exists(_installDirectory))
                Directory.Delete(_installDirectory, true);

            Directory.CreateDirectory(_installDirectory);

            foreach (var directory in Directory.GetDirectories(extractPath, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(extractPath, directory);
                Directory.CreateDirectory(Path.Combine(_installDirectory, relative));
            }

            foreach (var file in Directory.GetFiles(extractPath, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(extractPath, file);
                var destination = Path.Combine(_installDirectory, relative);
                var destinationDir = Path.GetDirectoryName(destination);
                if (!string.IsNullOrWhiteSpace(destinationDir))
                    Directory.CreateDirectory(destinationDir);
                File.Copy(file, destination, true);
            }

            File.WriteAllText(Path.Combine(_installDirectory, VersionFileName), tagName);

            if (!File.Exists(ExecutablePath))
                return ThirdPartyToolInstallResult.Failure("Архив скачан, но nvidiaProfileInspector.exe не найден после распаковки.");

            return ThirdPartyToolInstallResult.Success($"NVIDIA Inspector установлен ({tagName}).");
        }
        catch (Exception ex)
        {
            return ThirdPartyToolInstallResult.Failure($"Не удалось установить NVIDIA Inspector: {ex.Message}");
        }
    }

    public bool OpenInstallFolder()
    {
        try
        {
            Directory.CreateDirectory(_installDirectory);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{_installDirectory}\"",
                UseShellExecute = true
            });
            return true;
        }
        catch
        {
            return false;
        }
    }
}

public sealed class ThirdPartyToolInstallResult
{
    public bool IsSuccess { get; init; }

    public string Message { get; init; } = string.Empty;

    public static ThirdPartyToolInstallResult Success(string message)
    {
        return new ThirdPartyToolInstallResult { IsSuccess = true, Message = message };
    }

    public static ThirdPartyToolInstallResult Failure(string message)
    {
        return new ThirdPartyToolInstallResult { IsSuccess = false, Message = message };
    }
}
