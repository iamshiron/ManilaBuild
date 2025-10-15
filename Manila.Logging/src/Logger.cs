using System.Collections.Concurrent;
using System.Linq;

namespace Shiron.Manila.Logging;

/// <summary>
/// Provides a way to manage a stack of logging contexts for an execution flow.
/// </summary>
/// <remarks>
/// This implementation is thread-safe and avoids race conditions by treating the
/// context stack as immutable. It uses <see cref="AsyncLocal{T}"/> to maintain a separate
/// context stack for each asynchronous control flow (e.g., an async method or a thread).
/// </remarks>
public class LogContext {
    /// <summary>
    /// Stores the stack of context GUIDs for the current asynchronous control flow.
    /// </summary>
    private readonly AsyncLocal<Stack<Guid>> _contextStack = new();

    public Guid? CurrentContextID => _contextStack.Value?.Count > 0 ? _contextStack.Value.Peek() : null;

    /// <summary>
    /// Pushes a new context ID onto the stack in a thread-safe manner.
    /// </summary>
    /// <param name="contextId">The ID of the context to push.</param>
    /// <returns>An <see cref="IDisposable"/> that will restore the previous context when disposed.</returns>
    /// <example>
    /// <code>
    /// using (LogContext.PushContext(Guid.NewGuid()))
    /// {
    ///     // All logs within this block will be associated with the new context.
    /// }
    /// // The context is automatically restored here.
    /// </code>
    /// </example>
    public IDisposable PushContext(Guid contextId) {
        var originalStack = _contextStack.Value;
        var newStack = originalStack == null
            ? new Stack<Guid>()
            : new Stack<Guid>(originalStack.Reverse());

        newStack.Push(contextId);
        _contextStack.Value = newStack;
        return new ContextRestorer(this, originalStack ?? new Stack<Guid>());
    }

    public IDisposable PushContext(out Guid contextID) {
        contextID = Guid.NewGuid();
        return PushContext(contextID);
    }

    /// <summary>
    /// A private helper class that restores the previous context stack when disposed.
    /// This enables a 'using' pattern for safe context management.
    /// </summary>
    private sealed class ContextRestorer(LogContext context, Stack<Guid> stackToRestore) : IDisposable {
        private readonly LogContext _owner = context;
        private readonly Stack<Guid> _stackToRestore = stackToRestore;

        /// <summary>
        /// Restores the previous context stack for the current async context.
        /// </summary>
        public void Dispose() {
            _owner._contextStack.Value = _stackToRestore;
        }
    }
}

/// <summary>
/// The internal logger for Manila. Plugins should use their own logger.
/// </summary>
/// <remarks>
/// See <c>PluginInfo(Attributes.ManilaPlugin, object[])</c> as an example.
/// </remarks>
public class Logger(string? loggerPrefix) : ILogger {
    /// <summary>
    /// Event that is raised whenever a log entry is created.
    /// Subscribers can handle this event to process log entries, such as writing them to a file or displaying them in the console.
    /// </summary>
    public event Action<ILogEntry>? OnLogEntry;

    /// <summary>
    /// A concurrent dictionary holding the currently active log injectors, keyed by their unique ID.
    /// Using a thread-safe collection prevents race conditions when jobs log in parallel while
    /// injectors are being added/removed.
    /// </summary>
    private readonly ConcurrentDictionary<Guid, LogInjector> _activeInjectors = new();
    public LogContext LogContext { get; } = new();

    public string? LoggerPrefix => loggerPrefix;

    /// <summary>
    /// Registers a log injector to receive all log entries.
    /// </summary>
    /// <param name="id">The unique identifier of the injector.</param>
    /// <param name="injector">The log injector instance.</param>
    /// <exception cref="InvalidOperationException">Thrown if an injector with the same ID already exists.</exception>
    public void AddInjector(Guid id, LogInjector injector) {
        if (!_activeInjectors.TryAdd(id, injector)) {
            throw new Exception($"An injector with ID {id} already exists.");
        }
    }

    /// <summary>
    /// Removes a log injector, preventing it from receiving future log entries.
    /// </summary>
    /// <param name="id">The unique identifier of the injector to remove.</param>
    /// <exception cref="InvalidOperationException">Thrown if no injector with the specified ID is found.</exception>
    public void RemoveInjector(Guid id) {
        if (!_activeInjectors.TryRemove(id, out _)) {
            // Note: Depending on requirements, this could fail silently instead of throwing.
            // Throwing makes behavior more explicit.
            throw new Exception($"No injector with ID {id} exists to remove.");
        }
    }

    /// <summary>
    /// Logs a log entry by invoking the main <see cref="OnLogEntry"/> event and all active injectors.
    /// </summary>
    /// <param name="entry">The log entry to process.</param>
    public void Log(ILogEntry entry) {
        if (entry.ParentContextID == null && LogContext.CurrentContextID != null)
            entry.ParentContextID = LogContext.CurrentContextID;

        OnLogEntry?.Invoke(entry);
        // Iterate over a thread-safe snapshot of injectors. ConcurrentDictionary supports safe enumeration.
        foreach (var injector in _activeInjectors.Values) {
            injector.Handle(entry);
        }
    }

    /// <summary>
    /// Logs a message as a markup line at the Info level.
    /// </summary>
    /// <param name="message">The message to log.</param>
    public void MarkupLine(string message, bool logAlways = false) {
        Log(new MarkupLogEntry(message, logAlways, loggerPrefix));
    }

    /// <summary>
    /// Logs a message at the Info level.
    /// </summary>
    /// <param name="message">The message to log.</param>
    public void Info(string message) {
        Log(new BasicLogEntry(message, LogLevel.Info, loggerPrefix));
    }

    /// <summary>
    /// Logs a message at the Debug level.
    /// </summary>
    /// <param name="message">The message to log.</param>
    public void Debug(string message) {
        Log(new BasicLogEntry(message, LogLevel.Debug, loggerPrefix));
    }

    /// <summary>
    /// Logs a message at the Warning level.
    /// </summary>
    /// <param name="message">The message to log.</param>
    public void Warning(string message) {
        Log(new BasicLogEntry(message, LogLevel.Warning, loggerPrefix));
    }

    /// <summary>
    /// Logs a message at the Error level.
    /// </summary>
    /// <param name="message">The message to log.</param>
    public void Error(string message) {
        Log(new BasicLogEntry(message, LogLevel.Error, loggerPrefix));
    }

    /// <summary>
    /// Logs a message at the Critical level.
    /// </summary>
    /// <param name="message">The message to log.</param>
    public void Critical(string message) {
        Log(new BasicLogEntry(message, LogLevel.Critical, loggerPrefix));
    }

    /// <summary>
    /// Logs a message at the System level.
    /// </summary>
    /// <param name="message">The message to log.</param>
    public void System(string message) {
        Log(new BasicLogEntry(message, LogLevel.System, loggerPrefix));
    }
}

public static class LoggerExtensions {
    /// <summary>
    /// Creates a LogInjector that only receives entries matching the provided ParentContextID.
    /// </summary>
    /// <param name="logger">The logger to attach to.</param>
    /// <param name="onLog">Callback to handle matching entries.</param>
    /// <param name="contextId">The context identifier to filter on.</param>
    public static LogInjector CreateContextInjector(this ILogger logger, Action<ILogEntry> onLog, Guid contextId) {
        return new LogInjector(logger, onLog, entry => entry.ParentContextID == contextId);
    }
}
