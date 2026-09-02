using System;
using System.IO;

namespace ArbuzTweaker;

public sealed class AppLogService
{
    // Лог раньше только дописывался и рос без предела. Теперь при превышении размера
    // текущий файл уходит в arbuz-tweaker.1.log, тот — в .2.log, самый старый удаляется.
    private const long MaxLogBytes = 1024 * 1024;
    private const int ArchivedLogCount = 2;

    private readonly object _syncRoot = new();
    private readonly string _logDirectory;

    public AppLogService(ConfigService configService)
    {
        _logDirectory = Path.Combine(configService.AppDataPath, "Logs");
    }

    public string LogDirectory => _logDirectory;

    public string LogFilePath => Path.Combine(_logDirectory, "arbuz-tweaker.log");

    public void Info(string message)
    {
        Write("INFO", message);
    }

    public void Error(string message, Exception? exception = null)
    {
        Write("ERROR", exception == null ? message : message + Environment.NewLine + exception);
    }

    private void Write(string level, string message)
    {
        try
        {
            Directory.CreateDirectory(_logDirectory);
            var line = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}] [{level}] {message}{Environment.NewLine}";

            lock (_syncRoot)
            {
                RotateIfNeeded();
                File.AppendAllText(LogFilePath, line);
            }
        }
        catch
        {
        }
    }

    private void RotateIfNeeded()
    {
        try
        {
            var current = new FileInfo(LogFilePath);
            if (!current.Exists || current.Length < MaxLogBytes)
                return;

            var oldest = ArchivePath(ArchivedLogCount);
            if (File.Exists(oldest))
                File.Delete(oldest);

            for (var index = ArchivedLogCount - 1; index >= 1; index--)
            {
                var from = ArchivePath(index);
                if (File.Exists(from))
                    File.Move(from, ArchivePath(index + 1), true);
            }

            File.Move(LogFilePath, ArchivePath(1), true);
        }
        catch
        {
            // Если ротация не удалась, просто продолжаем писать в текущий файл.
        }
    }

    private string ArchivePath(int index)
    {
        return Path.Combine(_logDirectory, $"arbuz-tweaker.{index}.log");
    }
}
