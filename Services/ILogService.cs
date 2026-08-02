using System.Diagnostics;

namespace NileFusion.Converter.Services;

/// <summary>
/// Simple logging service for application diagnostics.
/// </summary>
public interface ILogService
{
    void Log(string message, LogLevel level = LogLevel.Info);
    void LogError(string message, Exception? ex = null);
    void LogWarning(string message);
    void LogInfo(string message);
    void LogDebug(string message);
}

/// <summary>
/// Log level enumeration.
/// </summary>
public enum LogLevel
{
    Debug = 0,
    Info = 1,
    Warning = 2,
    Error = 3
}

/// <summary>
/// Implementation of logging service with file and debug output.
/// </summary>
public class FileLogService : ILogService
{
    private readonly string _logFilePath;
    private readonly object _lockObj = new();
    private const int MaxLogFileSizeBytes = 5_000_000; // 5 MB

    public FileLogService()
    {
        string logsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        if (!Directory.Exists(logsFolder))
            Directory.CreateDirectory(logsFolder);

        _logFilePath = Path.Combine(logsFolder, $"app_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log");
    }

    public void Log(string message, LogLevel level = LogLevel.Info)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        lock (_lockObj)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                string logEntry = $"[{timestamp}] [{level}] {message}";

                // Write to debug output
                Debug.WriteLine(logEntry);

                // Write to file with rotation check
                if (File.Exists(_logFilePath) && new FileInfo(_logFilePath).Length > MaxLogFileSizeBytes)
                {
                    string backupPath = _logFilePath.Replace(".log", $"_{DateTime.Now:HHmmss}.log");
                    File.Move(_logFilePath, backupPath);
                }

                File.AppendAllText(_logFilePath, logEntry + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to write to log file: {ex.Message}");
            }
        }
    }

    public void LogError(string message, Exception? ex = null)
    {
        string fullMessage = ex == null ? message : $"{message}\nException: {ex}";
        Log(fullMessage, LogLevel.Error);
    }

    public void LogWarning(string message) => Log(message, LogLevel.Warning);
    public void LogInfo(string message) => Log(message, LogLevel.Info);
    public void LogDebug(string message) => Log(message, LogLevel.Debug);
}

/// <summary>
/// Null object pattern for logging (no-op).
/// </summary>
public class NullLogService : ILogService
{
    public void Log(string message, LogLevel level = LogLevel.Info) { }
    public void LogError(string message, Exception? ex = null) { }
    public void LogWarning(string message) { }
    public void LogInfo(string message) { }
    public void LogDebug(string message) { }
}
