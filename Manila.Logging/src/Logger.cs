using System.Collections.Concurrent;
using System.Linq;

namespace Shiron.Manila.Logging;

/// <summary>Async-local context GUID stack.</summary>
public class LogContext {
    /// <summary>Async-local stack.</summary>
    private readonly AsyncLocal<Stack<Guid>> _contextStack = new();

    public Guid? CurrentContextID => _contextStack.Value?.Count > 0 ? _contextStack.Value.Peek() : null;

    /// <summary>Push context.</summary>
    /// <param name="contextId">Context GUID.</param>
    /// <returns>Disposable restorer.</returns>
    public IDisposable PushContext(Guid contextId) {
        var originalStack = _contextStack.Value;
        var newStack = originalStack == null
            ? new Stack<Guid>()
            : new Stack<Guid>(originalStack.Reverse());

        newStack.Push(contextId);
        _contextStack.Value = newStack;
        return new ContextRestorer(this, originalStack ?? new Stack<Guid>());
    }

    /// <summary>Generate + push GUID.</summary>
    /// <param name="contextID">Generated GUID.</param>
    /// <returns>Disposable restorer.</returns>
    public IDisposable PushContext(out Guid contextID) { contextID = Guid.NewGuid(); return PushContext(contextID); }

    /// <summary>Restorer disposable.</summary>
    private sealed class ContextRestorer(LogContext context, Stack<Guid> stackToRestore) : IDisposable {
        private readonly LogContext _owner = context;
        private readonly Stack<Guid> _stackToRestore = stackToRestore;

        /// <summary>Restore stack.</summary>
        public void Dispose() { _owner._contextStack.Value = _stackToRestore; }
    }
}

/// <summary>Internal Manila logger.</summary>
public class Logger(string? loggerPrefix) : ILogger {
    /// <summary>Entry event.</summary>
    public event Action<ILogEntry>? OnLogEntry;

    /// <summary>Injector map.</summary>
    private readonly ConcurrentDictionary<Guid, LogInjector> _activeInjectors = new();
    public LogContext LogContext { get; } = new();

    public string? LoggerPrefix => loggerPrefix;

    /// <summary>Add injector.</summary>
    /// <param name="id">Injector ID.</param>
    /// <param name="injector">Injector instance.</param>
    /// <exception cref="InvalidOperationException">Thrown if an injector with the same ID already exists.</exception>
    public void AddInjector(Guid id, LogInjector injector) {
        if (!_activeInjectors.TryAdd(id, injector)) {
            throw new Exception($"An injector with ID {id} already exists.");
        }
    }

    /// <summary>Remove injector.</summary>
    /// <param name="id">Injector ID.</param>
    /// <exception cref="InvalidOperationException">Thrown if no injector with the specified ID is found.</exception>
    public void RemoveInjector(Guid id) {
        if (!_activeInjectors.TryRemove(id, out _)) {
            // Note: Depending on requirements, this could fail silently instead of throwing.
            // Throwing makes behavior more explicit.
            throw new Exception($"No injector with ID {id} exists to remove.");
        }
    }

    /// <summary>Emit entry.</summary>
    /// <param name="entry">Entry object.</param>
    public void Log(ILogEntry entry) {
        if (entry.ParentContextID == null && LogContext.CurrentContextID != null)
            entry.ParentContextID = LogContext.CurrentContextID;

        OnLogEntry?.Invoke(entry);
        // Iterate over a thread-safe snapshot of injectors. ConcurrentDictionary supports safe enumeration.
        foreach (var injector in _activeInjectors.Values) {
            injector.Handle(entry);
        }
    }

    /// <summary>Markup info line.</summary>
    /// <param name="message">Message text.</param>
    public void MarkupLine(string message, bool logAlways = false) {
        Log(new MarkupLogEntry(message, logAlways, loggerPrefix));
    }

    /// <summary>Info.</summary>
    public void Info(string message) {
        Log(new BasicLogEntry(message, LogLevel.Info, loggerPrefix));
    }

    /// <summary>Debug.</summary>
    public void Debug(string message) {
        Log(new BasicLogEntry(message, LogLevel.Debug, loggerPrefix));
    }

    /// <summary>Warning.</summary>
    public void Warning(string message) {
        Log(new BasicLogEntry(message, LogLevel.Warning, loggerPrefix));
    }

    /// <summary>Error.</summary>
    public void Error(string message) {
        Log(new BasicLogEntry(message, LogLevel.Error, loggerPrefix));
    }

    /// <summary>Critical.</summary>
    public void Critical(string message) {
        Log(new BasicLogEntry(message, LogLevel.Critical, loggerPrefix));
    }

    /// <summary>System.</summary>
    public void System(string message) {
        Log(new BasicLogEntry(message, LogLevel.System, loggerPrefix));
    }
}

/// <summary>Logger extensions.</summary>
public static class LoggerExtensions {
    /// <summary>Create context injector.</summary>
    /// <param name="logger">Logger.</param>
    /// <param name="onLog">Callback.</param>
    /// <param name="contextId">Context GUID.</param>
    /// <returns>Injector instance.</returns>
    public static LogInjector CreateContextInjector(this ILogger logger, Action<ILogEntry> onLog, Guid contextId) => new(logger, onLog, entry => entry.ParentContextID == contextId);
}
