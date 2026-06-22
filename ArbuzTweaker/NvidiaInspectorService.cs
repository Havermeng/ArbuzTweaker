using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;

namespace ArbuzTweaker;

public sealed class NvidiaInspectorService
{
    private const string Owner = "Orbmu2k";
    private const string Repo = "nvidiaProfileInspector";
    private const string AssetName = "nvidiaProfileInspector.zip";
    private const string VersionFileName = ".version";
    private static readonly TimeSpan NetworkTimeout = TimeSpan.FromSeconds(90);

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
            using var client = new HttpClient
            {
                Timeout = NetworkTimeout
            };
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
            var stagingInstallDirectory = _installDirectory + ".new";
            var backupInstallDirectory = _installDirectory + ".old";

            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, true);

            if (Directory.Exists(stagingInstallDirectory))
                Directory.Delete(stagingInstallDirectory, true);

            if (Directory.Exists(backupInstallDirectory))
                Directory.Delete(backupInstallDirectory, true);

            Directory.CreateDirectory(tempRoot);
            Directory.CreateDirectory(extractPath);

            await using (var zipStream = await client.GetStreamAsync(downloadUrl))
            await using (var fileStream = File.Create(zipPath))
            {
                await zipStream.CopyToAsync(fileStream);
            }

            var archiveSha256 = ComputeSha256(zipPath);

            ZipFile.ExtractToDirectory(zipPath, extractPath, true);

            Directory.CreateDirectory(stagingInstallDirectory);

            foreach (var directory in Directory.GetDirectories(extractPath, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(extractPath, directory);
                Directory.CreateDirectory(Path.Combine(stagingInstallDirectory, relative));
            }

            foreach (var file in Directory.GetFiles(extractPath, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(extractPath, file);
                var destination = Path.Combine(stagingInstallDirectory, relative);
                var destinationDir = Path.GetDirectoryName(destination);
                if (!string.IsNullOrWhiteSpace(destinationDir))
                    Directory.CreateDirectory(destinationDir);
                File.Copy(file, destination, true);
            }

            File.WriteAllText(Path.Combine(stagingInstallDirectory, VersionFileName), tagName);

            var stagedExecutablePath = Path.Combine(stagingInstallDirectory, "nvidiaProfileInspector.exe");
            if (!File.Exists(stagedExecutablePath))
            {
                Directory.Delete(stagingInstallDirectory, true);
                return ThirdPartyToolInstallResult.Failure("Архив скачан, но nvidiaProfileInspector.exe не найден после распаковки.");
            }

            var backupCreated = false;
            try
            {
                if (Directory.Exists(_installDirectory))
                {
                    Directory.Move(_installDirectory, backupInstallDirectory);
                    backupCreated = true;
                }

                Directory.Move(stagingInstallDirectory, _installDirectory);
            }
            catch
            {
                if (backupCreated && !Directory.Exists(_installDirectory) && Directory.Exists(backupInstallDirectory))
                    Directory.Move(backupInstallDirectory, _installDirectory);

                throw;
            }

            if (Directory.Exists(backupInstallDirectory))
                Directory.Delete(backupInstallDirectory, true);

            return ThirdPartyToolInstallResult.Success($"NVIDIA Inspector установлен ({tagName}). SHA256 архива: {archiveSha256}");
        }
        catch (Exception ex)
        {
            return ThirdPartyToolInstallResult.Failure($"Не удалось установить NVIDIA Inspector: {ex.Message}");
        }
    }

    private static string ComputeSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return Convert.ToHexString(SHA256.HashData(stream));
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
