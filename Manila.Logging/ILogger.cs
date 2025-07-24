
namespace Shiron.Manila.Logging;

public interface ILogger {
    public void Log(ILogEntry entry);

    /// <summary>
    /// Writes a message to the log with markup formatting.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="logAlways">If true, the message will be logged regardless of the current log level.</param>
    public void MarkupLine(string message, bool logAlways = false);
    public void Info(string message);
    public void Debug(string message);
    public void Warning(string message);
    public void Error(string message);
    public void Critical(string message);
    public void System(string message);

    public void AddInjector(Guid id, LogInjector injector);
    public void RemoveInjector(Guid id);

    public string? LoggerPrefix { get; }

    public LogContext LogContext { get; }

    public event Action<ILogEntry>? OnLogEntry;
}
