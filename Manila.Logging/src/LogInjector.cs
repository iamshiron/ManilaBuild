namespace Shiron.Manila.Logging;

/// <summary>
/// Represents a log injector that allows custom actions to be performed whenever a log entry is created.
/// </summary>
/// <remarks>
/// This class should be disposed to unregister the injector and prevent memory leaks.
/// </remarks>
public class LogInjector : IDisposable {
    /// <summary>
    /// Gets the unique identifier for this log injector instance.
    /// </summary>
    public readonly Guid ID = Guid.NewGuid();

    internal readonly Action<ILogEntry> _onLog;
    private readonly Func<ILogEntry, bool>? _filter;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LogInjector"/> class.
    /// </summary>
    /// <param name="onLog">The action to execute for each new log entry. Cannot be null.</param>
    public LogInjector(ILogger logger, Action<ILogEntry> onLog) : this(logger, onLog, null) { }

    /// <summary>
    /// Initializes a new instance with an optional filter to select which log entries this injector should receive.
    /// </summary>
    /// <param name="logger">The logger to attach to.</param>
    /// <param name="onLog">Callback invoked for each matching log entry.</param>
    /// <param name="filter">Optional predicate to filter entries; if null, all entries are passed.</param>
    public LogInjector(ILogger logger, Action<ILogEntry> onLog, Func<ILogEntry, bool>? filter) {
        _onLog = onLog ?? throw new Exception(nameof(onLog));
        _logger = logger ?? throw new Exception(nameof(logger));
        _filter = filter;
        _logger.AddInjector(ID, this);
    }

    /// <summary>
    /// Called by the logger to process a log entry. Applies filtering before invoking the callback.
    /// </summary>
    /// <param name="entry">The log entry to handle.</param>
    internal void Handle(ILogEntry entry) {
        if (_filter == null || _filter(entry)) {
            _onLog(entry);
        }
    }

    /// <summary>
    /// Removes this injector from the logger, stopping it from receiving future log entries.
    /// </summary>
    public void Dispose() {
        try {
            _logger.RemoveInjector(ID);
        } catch {
            // Swallow exceptions on dispose to avoid tearing down due to race conditions
            // (e.g., injector already removed or logger shutting down).
        }
    }
}
