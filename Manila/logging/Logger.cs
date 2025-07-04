
namespace Shiron.Manila.Logging;

/// <summary>
/// Provides a way to manage a stack of logging contexts for an execution flow.
/// This implementation is thread-safe and avoids race conditions by treating the
/// context stack as immutable.
/// </summary>
public static class LogContext {
    private static readonly AsyncLocal<Stack<Guid>> _contextStack = new();

    /// <summary>
    /// A private helper class that restores the previous context stack when disposed.
    /// </summary>
    private sealed class ContextRestorer : IDisposable {
        private readonly Stack<Guid> _stackToRestore;

        public ContextRestorer(Stack<Guid> stackToRestore) {
            _stackToRestore = stackToRestore;
        }

        public void Dispose() {
            // Restore the previous stack for the current async context.
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
    /// <returns>An IDisposable that will restore the previous context when disposed.</returns>
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
        return new ContextRestorer(originalStack);
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
