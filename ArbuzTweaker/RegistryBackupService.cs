using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;

namespace ArbuzTweaker;

public sealed class RegistryBackupService
{
    private readonly object _syncRoot = new();
    private readonly AppLogService _logService;
    private readonly string _backupDirectory;
    private readonly string _backupFilePath;

    public RegistryBackupService(ConfigService configService, AppLogService logService)
    {
        _logService = logService;
        _backupDirectory = Path.Combine(configService.AppDataPath, "Backups", "Registry");
        _backupFilePath = Path.Combine(_backupDirectory, "registry-backup.json");
    }

    public string BackupDirectory => _backupDirectory;

    public string BackupFilePath => _backupFilePath;

    public void CaptureDwordValue(string group, string rootName, string keyPath, string valueName)
    {
        CaptureValue(group, rootName, keyPath, valueName);
    }

    public void CaptureValue(string group, string rootName, string keyPath, string valueName)
    {
        try
        {
            lock (_syncRoot)
            {
                var backup = LoadBackup();
                var key = RegistryBackupEntry.CreateKey(rootName, keyPath, valueName);
                if (backup.Entries.ContainsKey(key))
                    return;

                using var registryKey = OpenRoot(rootName).OpenSubKey(keyPath, false);
                var value = registryKey?.GetValue(valueName);
                var valueKind = GetValueKind(registryKey, valueName);
                backup.Entries[key] = new RegistryBackupEntry
                {
                    Group = group,
                    RootName = rootName,
                    KeyPath = keyPath,
                    ValueName = valueName,
                    Existed = value != null,
                    ValueKind = valueKind,
                    DwordValue = value is int intValue ? intValue : null,
                    StringValue = value is string stringValue ? stringValue : null,
                    QwordValue = value is long longValue ? longValue : null,
                    BinaryValue = value as byte[],
                    MultiStringValue = value as string[],
                    CapturedAt = DateTimeOffset.Now
                };

                SaveBackup(backup);
            }

            _logService.Info($"Registry backup captured: {rootName}\\{keyPath}\\{valueName}");
        }
        catch (Exception ex)
        {
            _logService.Error($"Failed to capture registry backup for {rootName}\\{keyPath}\\{valueName}", ex);
        }
    }

    public RegistryRestoreResult RestoreAll()
    {
        var backup = LoadBackup();
        var restored = 0;
        var failed = 0;

        foreach (var entry in backup.Entries.Values)
        {
            try
            {
                if (entry.Existed)
                {
                    using var key = OpenRoot(entry.RootName).CreateSubKey(entry.KeyPath, true);
                    if (key == null || !TryRestoreValue(key, entry))
                    {
                        failed++;
                        continue;
                    }
                }
                else
                {
                    using var key = OpenRoot(entry.RootName).OpenSubKey(entry.KeyPath, true);
                    if (key?.GetValue(entry.ValueName) != null)
                        key.DeleteValue(entry.ValueName, false);
                }

                restored++;
            }
            catch (Exception ex)
            {
                failed++;
                _logService.Error($"Failed to restore registry value {entry.RootName}\\{entry.KeyPath}\\{entry.ValueName}", ex);
            }
        }

        return new RegistryRestoreResult(restored, failed, backup.Entries.Count);
    }

    public bool OpenBackupFolder()
    {
        try
        {
            Directory.CreateDirectory(_backupDirectory);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{_backupDirectory}\"",
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception ex)
        {
            _logService.Error("Failed to open registry backup folder.", ex);
            return false;
        }
    }

    private RegistryBackupData LoadBackup()
    {
        try
        {
            if (!File.Exists(_backupFilePath))
                return new RegistryBackupData();

            var json = File.ReadAllText(_backupFilePath);
            var backup = JsonSerializer.Deserialize<RegistryBackupData>(json) ?? new RegistryBackupData();

            // System.Text.Json создаёт словарь с регистрозависимым компаратором —
            // восстанавливаем OrdinalIgnoreCase, на нём держится защита от повторной записи.
            backup.Entries = new Dictionary<string, RegistryBackupEntry>(backup.Entries, StringComparer.OrdinalIgnoreCase);
            return backup;
        }
        catch (Exception ex)
        {
            _logService.Error("Failed to read registry backup file.", ex);
            return new RegistryBackupData();
        }
    }

    private void SaveBackup(RegistryBackupData backup)
    {
        Directory.CreateDirectory(_backupDirectory);
        var json = JsonSerializer.Serialize(backup, new JsonSerializerOptions { WriteIndented = true });
        var tempPath = _backupFilePath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, _backupFilePath, true);
    }

    private static RegistryKey OpenRoot(string rootName)
    {
        return rootName.Equals("HKCU", StringComparison.OrdinalIgnoreCase)
            ? Registry.CurrentUser
            : Registry.LocalMachine;
    }

    private static RegistryValueKind? GetValueKind(RegistryKey? key, string valueName)
    {
        try
        {
            return key?.GetValue(valueName) == null ? null : key.GetValueKind(valueName);
        }
        catch
        {
            return null;
        }
    }

    private static bool TryRestoreValue(RegistryKey key, RegistryBackupEntry entry)
    {
        if (entry.DwordValue.HasValue)
        {
            key.SetValue(entry.ValueName, entry.DwordValue.Value, RegistryValueKind.DWord);
            return true;
        }

        if (entry.StringValue != null)
        {
            var valueKind = entry.ValueKind is RegistryValueKind.ExpandString
                ? RegistryValueKind.ExpandString
                : RegistryValueKind.String;

            key.SetValue(entry.ValueName, entry.StringValue, valueKind);
            return true;
        }

        if (entry.QwordValue.HasValue)
        {
            key.SetValue(entry.ValueName, entry.QwordValue.Value, RegistryValueKind.QWord);
            return true;
        }

        if (entry.BinaryValue != null)
        {
            key.SetValue(entry.ValueName, entry.BinaryValue, RegistryValueKind.Binary);
            return true;
        }

        if (entry.MultiStringValue != null)
        {
            key.SetValue(entry.ValueName, entry.MultiStringValue, RegistryValueKind.MultiString);
            return true;
        }

        return false;
    }
}

public sealed class RegistryBackupData
{
    public Dictionary<string, RegistryBackupEntry> Entries { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class RegistryBackupEntry
{
    public string Group { get; set; } = string.Empty;

    public string RootName { get; set; } = string.Empty;

    public string KeyPath { get; set; } = string.Empty;

    public string ValueName { get; set; } = string.Empty;

    public bool Existed { get; set; }

    public RegistryValueKind? ValueKind { get; set; }

    public int? DwordValue { get; set; }

    public string? StringValue { get; set; }

    public long? QwordValue { get; set; }

    public byte[]? BinaryValue { get; set; }

    public string[]? MultiStringValue { get; set; }

    public DateTimeOffset CapturedAt { get; set; }

    public static string CreateKey(string rootName, string keyPath, string valueName)
    {
        return $"{rootName}\\{keyPath}\\{valueName}";
    }
}

public sealed record RegistryRestoreResult(int Restored, int Failed, int Total);
