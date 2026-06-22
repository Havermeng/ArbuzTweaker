using System;
using System.IO;

namespace ArbuzTweaker;

public sealed class AppLogService
{
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
                File.AppendAllText(LogFilePath, line);
            }
        }
        catch
        {
        }
    }
}
