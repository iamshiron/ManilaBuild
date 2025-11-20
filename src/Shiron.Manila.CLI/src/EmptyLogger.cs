

using System.Diagnostics.CodeAnalysis;
using Shiron.Logging;
using Shiron.Logging.Renderer;

namespace Shiron.Manila.CLI;

/// <summary>
/// No-op logger implementation used to suppress output
/// </summary>
public class EmptyLogger : ILogger {
    /// <summary>
    /// Logger prefix (always null)
    /// </summary>
    public string? LoggerPrefix => null;

    /// <summary>
    /// Logging context (empty instance)
    /// </summary>
    public LogContext LogContext => new();

#pragma warning disable CS0067
    /// <summary>
    /// Raised on log entry (never invoked)
    /// </summary>
    public event Action<ILogEntry>? OnLogEntry;
#pragma warning restore CS0067

    /// <summary>
    /// Ignores markup line
    /// </summary>
    public void MarkupLine(string message, bool logAlways = false) { }
    /// <summary>
    /// Ignores injector add
    /// </summary>
    public void AddInjector(Guid id, LogInjector injector) { }
    /// <summary>
    /// Ignores critical log
    /// </summary>
    public void Critical(string message) { }
    /// <summary>
    /// Ignores debug log
    /// </summary>
    public void Debug(string message) { }
    /// <summary>
    /// Ignores error log
    /// </summary>
    public void Error(string message) { }
    /// <summary>
    /// Ignores info log
    /// </summary>
    public void Info(string message) { }
    /// <summary>
    /// Ignores structured log entry
    /// </summary>
    public void Log(ILogEntry entry) { }
    /// <summary>
    /// Ignores injector removal
    /// </summary>
    public void RemoveInjector(Guid id) { }
    /// <summary>
    /// Ignores system log
    /// </summary>
    public void System(string message) { }
    /// <summary>
    /// Ignores warning log
    /// </summary>
    public void Warning(string message) { }

    /// <summary>
    /// Ignores renderer addition
    /// </summary>
    public void AddRenderer(ILogRenderer renderer) { }
    /// <summary>
    /// Creates a sub-logger with the same instance (no-op)
    /// </summary>
    public ILogger CreateSubLogger(string prefix) {
        return this; // Return the same instance for sub-logger
    }
}
