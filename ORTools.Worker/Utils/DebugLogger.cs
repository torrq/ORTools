using System.IO;
using System.Runtime.CompilerServices;

namespace ORTools.Worker;

/// <summary>
/// Thread-safe logger. Writes to console in all builds.
/// Wire LogMessageEmitted to WorkerCore.BroadcastAsync(LogMessageUpdate) in Phase 3
/// so the UI debug panel receives log entries.
/// </summary>
public static class DebugLogger
{
    public static event Action<string, string>? LogMessageEmitted; // (level, message)

    private static bool IsDebugMode =>
        AppConfig.DebugMode;

    public static void Info(string message)     => Emit(AppConfig.INFO,    message);
    public static void Warning(string message)  => Emit(AppConfig.WARNING, message);
    public static void Error(string message)    => Emit(AppConfig.ERROR,   message);
    public static void Status(string message)   => Emit(AppConfig.STATUS,  message);

    public static void Debug(string message)
    {
        if (IsDebugMode) Emit(AppConfig.DEBUG, message);
    }

    public static void Error(Exception ex, string context) =>
        Emit(AppConfig.ERROR, $"{context}: {ex.Message}");

    private static void Emit(string level, string message)
    {
        string line = $"[{DateTime.Now:HH:mm:ss}][{level}] {message}";
        Console.WriteLine(line);
        try
        {
            if (!string.IsNullOrEmpty(AppConfig.DebugLogFile))
                File.AppendAllText(AppConfig.DebugLogFile, line + Environment.NewLine);
        }
        catch { /* don't let logging failures crash threads */ }

        LogMessageEmitted?.Invoke(level, message);
    }
}
