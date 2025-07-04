
namespace Shiron.Manila.Logging;

public static class LogContext {
    // This will store a unique ID for the current execution flow.
    private static readonly AsyncLocal<Guid?> _currentContextId = new();

    public static Guid? CurrentContextId {
        get => _currentContextId.Value;
        set => _currentContextId.Value = value;
    }
}

/// <summary>
/// The internal logger for Manila. Plugins should use their own logger:
/// See <see cref="PluginInfo(Attributes.ManilaPlugin, object[])"/> as an example.
/// </summary>
public static class Logger {
    /// <summary>
    /// Event that is raised whenever a log entry is created.
    /// Subscribers can handle this event to process log entries, such as writing them to a file or displaying them in the console.
    /// The event provides the log entry as an argument.
    /// </summary>
    public static event Action<ILogEntry>? OnLogEntry;

    /// <summary>
    /// Logs a log entry.
    /// This method is the main entry point for logging messages.
    /// It raises the OnLogEntry event with the provided log entry.
    /// </summary>
    /// <param name="entry">The log entry.</param>
    public static void Log(ILogEntry entry) {
        OnLogEntry?.Invoke(entry);
    }

    /// <summary>
    /// Logs a message at the Info level.
    /// </summary>
    /// <param name="message">The message.</param>
    public static void Info(string message) {
        Log(new BasicLogEntry(message, LogLevel.Info));
    }
    /// <summary>
    /// Logs a message at the Debug level.
    /// </summary>
    /// <param name="message">The message.</param>
    public static void Debug(string message) {
        Log(new BasicLogEntry(message, LogLevel.Debug));
    }
    /// <summary>
    /// Logs a message at the Warning level.
    /// </summary>
    /// <param name="message">The message.</param>
    public static void Warning(string message) {
        Log(new BasicLogEntry(message, LogLevel.Warning));
    }
    /// <summary>
    /// Logs a message at the Error level.
    /// </summary>
    /// <param name="message">The message.</param>
    public static void Error(string message) {
        Log(new BasicLogEntry(message, LogLevel.Error));
    }
    /// <summary>
    /// Logs a message at the Critical level.
    /// </summary>
    /// <param name="message">The message.</param>
    public static void Critical(string message) {
        Log(new BasicLogEntry(message, LogLevel.Critical));
    }
    /// <summary>
    /// Logs a message at the System level.
    /// </summary>
    /// <param name="message">The message.</param>
    public static void System(string message) {
        Log(new BasicLogEntry(message, LogLevel.System));
    }
}
