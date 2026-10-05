using System.Collections.Concurrent;
using System.IO;
using System.Text;

namespace HPXPirate.Services;

public enum LogLevel { Debug, Info, Warn, Error, Critical }

public sealed record LogEntry(DateTime Timestamp, LogLevel Level, string Message);

public interface ILogger
{
    void Debug(string message);
    void Info(string message);
    void Warn(string message);
    void Error(string message, Exception? ex = null);
    void Critical(string message, Exception? ex = null);
    IReadOnlyList<LogEntry> Recent { get; }
    event Action<LogEntry>? EntryWritten;
}

/// <summary>Lightweight file + in-memory logger with size-based rotation.</summary>
public sealed class LoggingService : ILogger
{
    private readonly object _gate = new();
    private readonly string _logFile;
    private readonly ConcurrentQueue<LogEntry> _recent = new();
    private const int MaxRecent = 500;
    private const long MaxBytes = 2 * 1024 * 1024;

    public event Action<LogEntry>? EntryWritten;

    public LoggingService(string logDirectory)
    {
        Directory.CreateDirectory(logDirectory);
        _logFile = Path.Combine(logDirectory, "hpx_pirate.log");
        TryRotate();
    }

    public IReadOnlyList<LogEntry> Recent => _recent.ToArray();

    public void Debug(string message) => Write(LogLevel.Debug, message);
    public void Info(string message) => Write(LogLevel.Info, message);
    public void Warn(string message) => Write(LogLevel.Warn, message);
    public void Error(string message, Exception? ex = null) => Write(LogLevel.Error, message, ex);
    public void Critical(string message, Exception? ex = null) => Write(LogLevel.Critical, message, ex);

    private void Write(LogLevel level, string message, Exception? ex = null)
    {
        var entry = new LogEntry(DateTime.Now, level, ex is null ? message : $"{message} :: {ex.GetType().Name}: {ex.Message}");
        _recent.Enqueue(entry);
        while (_recent.Count > MaxRecent) _recent.TryDequeue(out _);

        try
        {
            var line = $"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{level,-8}] {entry.Message}";
            lock (_gate)
            {
                File.AppendAllText(_logFile, line + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch
        {
            // never let logging crash the app
        }

        EntryWritten?.Invoke(entry);
    }

    private void TryRotate()
    {
        try
        {
            if (File.Exists(_logFile) && new FileInfo(_logFile).Length > MaxBytes)
            {
                var bak = _logFile + ".1";
                if (File.Exists(bak)) File.Delete(bak);
                File.Move(_logFile, bak);
            }
        }
        catch { /* ignore */ }
    }

    public string LogFilePath => _logFile;
}
