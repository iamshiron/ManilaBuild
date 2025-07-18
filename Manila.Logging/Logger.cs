namespace Shiron.Manila.Logging;

/// <summary>
/// Provides a way to manage a stack of logging contexts for an execution flow.
/// </summary>
/// <remarks>
/// This implementation is thread-safe and avoids race conditions by treating the
/// context stack as immutable. It uses <see cref="AsyncLocal{T}"/> to maintain a separate
/// context stack for each asynchronous control flow (e.g., an async method or a thread).
/// </remarks>
public static class LogContext {
    /// <summary>
    /// Stores the stack of context GUIDs for the current asynchronous control flow.
    /// </summary>
    private static readonly AsyncLocal<Stack<Guid>> _contextStack = new();

    /// <summary>
    /// A private helper class that restores the previous context stack when disposed.
    /// This enables a 'using' pattern for safe context management.
    /// </summary>
    private sealed class ContextRestorer : IDisposable {
        /// <summary>
        /// The context stack that was active before a new context was pushed.
        /// </summary>
        private readonly Stack<Guid> _stackToRestore;

        /// <summary>
        /// Initializes a new instance of the <see cref="ContextRestorer"/> class.
        /// </summary>
        /// <param name="stackToRestore">The stack to restore when this instance is disposed.</param>
        public ContextRestorer(Stack<Guid> stackToRestore) {
            _stackToRestore = stackToRestore;
        }

        /// <summary>
        /// Restores the previous context stack for the current async context.
        /// </summary>
        public void Dispose() {
            _contextStack.Value = _stackToRestore;
        }
    }

    /// <summary>
    /// Gets the Guid of the current logical context.
    /// Returns null if no context is active.
    /// </summary>
    public static Guid? CurrentContextId {
        get {
            var stack = _contextStack.Value;
            return stack?.Count > 0 ? stack.Peek() : null;
        }
    }

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
    public static IDisposable PushContext(Guid contextId) {
        var originalStack = _contextStack.Value;

        // Create a NEW stack, copying the original. This is the crucial step
        // to prevent threads from sharing a mutable stack.
        var newStack = originalStack == null
            ? new Stack<Guid>()
            : new Stack<Guid>(originalStack.Reverse()); // Reverse is needed to maintain order

        // Push the new ID onto our new, isolated stack.
        newStack.Push(contextId);

        // Set the current context to our new stack.
        _contextStack.Value = newStack;

        // Return a restorer that knows how to put the original stack back.
        return new ContextRestorer(originalStack ?? new Stack<Guid>());
    }
}

/// <summary>
/// The internal logger for Manila. Plugins should use their own logger.
/// </summary>
/// <remarks>
/// See <c>PluginInfo(Attributes.ManilaPlugin, object[])</c> as an example.
/// </remarks>
public static class Logger {
    /// <summary>
    /// Event that is raised whenever a log entry is created.
    /// Subscribers can handle this event to process log entries, such as writing them to a file or displaying them in the console.
    /// </summary>
    public static event Action<ILogEntry>? OnLogEntry;

    /// <summary>
    /// A dictionary holding the currently active log injectors, keyed by their unique ID.
    /// </summary>
    private static readonly Dictionary<Guid, LogInjector> _activeInjectors = [];

    /// <summary>
    /// Registers a log injector to receive all log entries.
    /// </summary>
    /// <param name="id">The unique identifier of the injector.</param>
    /// <param name="injector">The log injector instance.</param>
    /// <exception cref="InvalidOperationException">Thrown if an injector with the same ID already exists.</exception>
    public static void AddInjector(Guid id, LogInjector injector) {
        if (!_activeInjectors.TryAdd(id, injector)) {
            throw new InvalidOperationException($"An injector with ID {id} already exists.");
        }
    }

    /// <summary>
    /// Unregisters a log injector, preventing it from receiving future log entries.
    /// </summary>
    /// <param name="id">The unique identifier of the injector to remove.</param>
    /// <exception cref="InvalidOperationException">Thrown if no injector with the specified ID is found.</exception>
    public static void RemoveInjector(Guid id) {
        if (!_activeInjectors.Remove(id)) {
            // Note: Depending on requirements, this could fail silently instead of throwing.
            // Throwing makes behavior more explicit.
            throw new InvalidOperationException($"No injector with ID {id} exists to remove.");
        }
    }

    /// <summary>
    /// Logs a log entry by invoking the main <see cref="OnLogEntry"/> event and all active injectors.
    /// </summary>
    /// <param name="entry">The log entry to process.</param>
    public static void Log(ILogEntry entry) {
        OnLogEntry?.Invoke(entry);
        // Create a copy of values to prevent collection modification issues if an injector modifies the collection.
        foreach (var injector in _activeInjectors.Values.ToList()) {
            injector._onLog(entry);
        }
    }

    /// <summary>
    /// Logs a message at the Info level.
    /// </summary>
    /// <param name="message">The message to log.</param>
    public static void Info(string message) {
        Log(new BasicLogEntry(message, LogLevel.Info));
    }

    /// <summary>
    /// Logs a message at the Debug level.
    /// </summary>
    /// <param name="message">The message to log.</param>
    public static void Debug(string message) {
        Log(new BasicLogEntry(message, LogLevel.Debug));
    }

    /// <summary>
    /// Logs a message at the Warning level.
    /// </summary>
    /// <param name="message">The message to log.</param>
    public static void Warning(string message) {
        Log(new BasicLogEntry(message, LogLevel.Warning));
    }

    /// <summary>
    /// Logs a message at the Error level.
    /// </summary>
    /// <param name="message">The message to log.</param>
    public static void Error(string message) {
        Log(new BasicLogEntry(message, LogLevel.Error));
    }

    /// <summary>
    /// Logs a message at the Critical level.
    /// </summary>
    /// <param name="message">The message to log.</param>
    public static void Critical(string message) {
        Log(new BasicLogEntry(message, LogLevel.Critical));
    }

    /// <summary>
    /// Logs a message at the System level.
    /// </summary>
    /// <param name="message">The message to log.</param>
    public static void System(string message) {
        Log(new BasicLogEntry(message, LogLevel.System));
    }
}
