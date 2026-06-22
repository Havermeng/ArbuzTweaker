using System.IO.Compression;
using System.Text.Json;

namespace ArbuzTweaker;

public sealed class ProfileService
{
    private readonly ConfigService _configService;
    private readonly AppLogService _logService;
    private readonly FileBackupService _fileBackupService;
    private readonly string _profileBackupRoot;

    public ProfileService(ConfigService configService, AppLogService logService, FileBackupService fileBackupService)
    {
        _configService = configService;
        _logService = logService;
        _fileBackupService = fileBackupService;
        _profileBackupRoot = Path.Combine(configService.AppDataPath, "Backups", "Profiles");
    }

    public void ExportProfile(string destinationZipPath)
    {
        Directory.CreateDirectory(_configService.ConfigsPath);

        var destinationDirectory = Path.GetDirectoryName(destinationZipPath);
        if (!string.IsNullOrWhiteSpace(destinationDirectory))
            Directory.CreateDirectory(destinationDirectory);

        if (File.Exists(destinationZipPath))
            File.Delete(destinationZipPath);

        using var archive = ZipFile.Open(destinationZipPath, ZipArchiveMode.Create);
        var metadata = JsonSerializer.Serialize(
            new ProfileMetadata(DateTimeOffset.Now, "ArbuzTweaker"),
            new JsonSerializerOptions { WriteIndented = true });

        var metadataEntry = archive.CreateEntry("profile.json", CompressionLevel.Optimal);
        using (var writer = new StreamWriter(metadataEntry.Open()))
            writer.Write(metadata);

        foreach (var filePath in Directory.EnumerateFiles(_configService.ConfigsPath, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(_configService.ConfigsPath, filePath);
            var entryPath = CombineZipPath("Configs", relativePath);
            archive.CreateEntryFromFile(filePath, entryPath, CompressionLevel.Optimal);
        }

        _logService.Info($"Profile exported: {destinationZipPath}");
    }

    public void ImportProfile(string sourceZipPath)
    {
        if (!File.Exists(sourceZipPath))
            throw new FileNotFoundException("Profile archive not found.", sourceZipPath);

        Directory.CreateDirectory(_configService.ConfigsPath);
        BackupCurrentConfigs();

        using var archive = ZipFile.OpenRead(sourceZipPath);
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Name))
                continue;

            if (!entry.FullName.StartsWith("Configs/", StringComparison.OrdinalIgnoreCase))
                continue;

            var relativePath = entry.FullName["Configs/".Length..].Replace('/', Path.DirectorySeparatorChar);
            if (string.IsNullOrWhiteSpace(relativePath))
                continue;

            var destinationPath = Path.GetFullPath(Path.Combine(_configService.ConfigsPath, relativePath));
            var configsRoot = Path.GetFullPath(_configService.ConfigsPath);
            if (!destinationPath.StartsWith(configsRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Profile archive contains an unsafe path.");

            var destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(destinationDirectory))
                Directory.CreateDirectory(destinationDirectory);

            if (File.Exists(destinationPath))
                _fileBackupService.BackupFile(destinationPath, "Profile import safety");

            entry.ExtractToFile(destinationPath, true);
        }

        _logService.Info($"Profile imported: {sourceZipPath}");
    }

    public bool OpenProfileBackupFolder()
    {
        try
        {
            Directory.CreateDirectory(_profileBackupRoot);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{_profileBackupRoot}\"",
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception ex)
        {
            _logService.Error("Failed to open profile backup folder.", ex);
            return false;
        }
    }

    private void BackupCurrentConfigs()
    {
        if (!Directory.Exists(_configService.ConfigsPath))
            return;

        var backupDirectory = Path.Combine(_profileBackupRoot, DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(backupDirectory);

        foreach (var filePath in Directory.EnumerateFiles(_configService.ConfigsPath, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(_configService.ConfigsPath, filePath);
            var backupPath = Path.Combine(backupDirectory, relativePath);
            var backupFileDirectory = Path.GetDirectoryName(backupPath);
            if (!string.IsNullOrWhiteSpace(backupFileDirectory))
                Directory.CreateDirectory(backupFileDirectory);

            File.Copy(filePath, backupPath, true);
        }
    }

    private static string CombineZipPath(string left, string right)
    {
        return $"{left}/{right.Replace('\\', '/')}";
    }

    private sealed record ProfileMetadata(DateTimeOffset ExportedAt, string AppName);
}
