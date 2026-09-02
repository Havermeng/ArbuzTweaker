using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ArbuzTweaker;

public sealed class FileBackupService
{
    private readonly AppLogService _logService;
    private readonly string _backupRoot;
    private readonly string _manifestPath;
    private readonly object _manifestLock = new();

    public FileBackupService(ConfigService configService, AppLogService logService)
    {
        _logService = logService;
        _backupRoot = Path.Combine(configService.AppDataPath, "Backups", "Files");
        _manifestPath = Path.Combine(_backupRoot, "manifest.json");
    }

    public string BackupRoot => _backupRoot;

    public string? BackupFile(string filePath, string category)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return null;

            var safeCategory = SanitizeFileName(category);
            var categoryDirectory = Path.Combine(_backupRoot, safeCategory);
            Directory.CreateDirectory(categoryDirectory);

            var fileName = Path.GetFileName(filePath);
            var backupName = $"{DateTime.Now:yyyyMMdd-HHmmss-fff}_{fileName}.bak";
            var backupPath = Path.Combine(categoryDirectory, backupName);

            File.Copy(filePath, backupPath, false);
            AddManifestEntry(new FileBackupEntry
            {
                Id = Guid.NewGuid().ToString("N"),
                Category = safeCategory,
                OriginalPath = Path.GetFullPath(filePath),
                BackupPath = Path.GetFullPath(backupPath),
                CreatedAt = DateTimeOffset.Now,
                OriginalSize = new FileInfo(filePath).Length,
                BackupSize = new FileInfo(backupPath).Length
            });

            _logService.Info($"Backup created: {filePath} -> {backupPath}");
            return backupPath;
        }
        catch (Exception ex)
        {
            _logService.Error($"Failed to create backup for {filePath}", ex);
            return null;
        }
    }

    public IReadOnlyList<FileBackupEntry> ListBackups()
    {
        try
        {
            return LoadManifest()
                .Where(entry => !string.IsNullOrWhiteSpace(entry.BackupPath) && File.Exists(entry.BackupPath))
                .OrderByDescending(entry => entry.CreatedAt)
                .ToArray();
        }
        catch (Exception ex)
        {
            _logService.Error("Failed to list file backups.", ex);
            return Array.Empty<FileBackupEntry>();
        }
    }

    public bool RestoreBackup(FileBackupEntry entry)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(entry.BackupPath) || !File.Exists(entry.BackupPath))
                return false;

            if (string.IsNullOrWhiteSpace(entry.OriginalPath))
                return false;

            var targetPath = Path.GetFullPath(entry.OriginalPath);
            var targetDirectory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrWhiteSpace(targetDirectory))
                Directory.CreateDirectory(targetDirectory);

            if (File.Exists(targetPath))
            {
                // Без проверки префикса повторные восстановления плодили категории
                // «Restore safety - Restore safety - ...» и всё более длинные пути папок.
                var safetyCategory = entry.Category.StartsWith("Restore safety", StringComparison.Ordinal)
                    ? entry.Category
                    : $"Restore safety - {entry.Category}";
                BackupFile(targetPath, safetyCategory);
            }

            File.Copy(entry.BackupPath, targetPath, true);
            _logService.Info($"Backup restored: {entry.BackupPath} -> {targetPath}");
            return true;
        }
        catch (Exception ex)
        {
            _logService.Error($"Failed to restore backup: {entry.BackupPath}", ex);
            return false;
        }
    }

    public bool OpenBackupFolder()
    {
        try
        {
            Directory.CreateDirectory(_backupRoot);
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{_backupRoot}\"",
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception ex)
        {
            _logService.Error("Failed to open backup folder.", ex);
            return false;
        }
    }

    // Сколько копий ОДНОГО файла держим. Раньше каждое «Применить» оставляло новый .bak
    // навсегда, и папка бэкапов росла без предела.
    private const int MaxBackupsPerFile = 10;

    private void AddManifestEntry(FileBackupEntry entry)
    {
        lock (_manifestLock)
        {
            var entries = LoadManifest().ToList();
            entries.Add(entry);
            PruneOldBackups(entries, entry.Category, entry.OriginalPath);
            SaveManifest(entries);
        }
    }

    // Удаляет самые старые копии этого же файла сверх лимита — и с диска, и из манифеста.
    private void PruneOldBackups(List<FileBackupEntry> entries, string category, string originalPath)
    {
        var stale = entries
            .Where(e => string.Equals(e.Category, category, StringComparison.OrdinalIgnoreCase)
                     && string.Equals(e.OriginalPath, originalPath, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(e => e.CreatedAt)
            .Skip(MaxBackupsPerFile)
            .ToList();

        foreach (var old in stale)
        {
            try
            {
                if (File.Exists(old.BackupPath))
                    File.Delete(old.BackupPath);
            }
            catch (Exception ex)
            {
                _logService.Error($"Failed to delete old backup: {old.BackupPath}", ex);
            }

            entries.Remove(old);
        }
    }

    private IReadOnlyList<FileBackupEntry> LoadManifest()
    {
        try
        {
            Directory.CreateDirectory(_backupRoot);
            if (!File.Exists(_manifestPath))
                return Array.Empty<FileBackupEntry>();

            var content = File.ReadAllText(_manifestPath);
            return JsonSerializer.Deserialize<FileBackupEntry[]>(content) ?? Array.Empty<FileBackupEntry>();
        }
        catch (Exception ex)
        {
            _logService.Error("Failed to read file backup manifest.", ex);
            return Array.Empty<FileBackupEntry>();
        }
    }

    private void SaveManifest(IReadOnlyList<FileBackupEntry> entries)
    {
        Directory.CreateDirectory(_backupRoot);
        var content = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_manifestPath, content);
    }

    private static string SanitizeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "General" : sanitized;
    }
}

public sealed class FileBackupEntry
{
    public string Id { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string OriginalPath { get; set; } = string.Empty;

    public string BackupPath { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public long OriginalSize { get; set; }

    public long BackupSize { get; set; }
}
