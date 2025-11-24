using System.Globalization;
using System.Text;
using System.Threading;

namespace YoutubeOcr.Core.Services;

/// <summary>
/// Minimal thread-safe file logger to capture unhandled exceptions and diagnostics.
/// </summary>
public static class FileLogger
{
    private static readonly object _lock = new();
    private static string _logDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "YoutubeOcr",
        "Logs");
    private static string _currentFile = Path.Combine(_logDirectory, $"app-{DateTime.Now:yyyyMMdd}.log");
    private static bool _initialized;

    public static void Initialize(string? directory = null)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(directory))
            {
                _logDirectory = directory!;
                _currentFile = Path.Combine(_logDirectory, $"app-{DateTime.Now:yyyyMMdd}.log");
            }

            Directory.CreateDirectory(_logDirectory);
            _initialized = true;
            LogInfo("Logger initialized");
        }
        catch
        {
            // swallow initialization errors to avoid crashing the app
        }
    }

    public static void LogInfo(string message) => Write("INFO", message);

    public static void LogError(Exception? ex, string message = "Error")
    {
        var sb = new StringBuilder();
        sb.Append(message);
        if (ex != null)
        {
            sb.Append(": ").Append(ex.GetType().FullName).Append(" - ").Append(ex.Message);
            sb.AppendLine();
            sb.Append(ex.StackTrace);
            if (ex.InnerException != null)
            {
                sb.AppendLine();
                sb.Append("Inner: ").Append(ex.InnerException.GetType().FullName).Append(" - ").Append(ex.InnerException.Message);
                sb.AppendLine();
                sb.Append(ex.InnerException.StackTrace);
            }
        }

        Write("ERROR", sb.ToString());
    }

    private static void Write(string level, string message)
    {
        if (!_initialized) return;
        try
        {
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [T{Thread.CurrentThread.ManagedThreadId}] [{level}] {message}";
            lock (_lock)
            {
                File.AppendAllText(_currentFile, line + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch
        {
            // never throw from logger
        }
    }
}
