namespace Shiron.Manila.Logging;

/// <summary>Entry callback injector.</summary>
public class LogInjector : IDisposable {
    /// <summary>Injector ID.</summary>
    public readonly Guid ID = Guid.NewGuid();

    internal readonly Action<ILogEntry> _onLog;
    private readonly Func<ILogEntry, bool>? _filter;
    private readonly ILogger _logger;

    /// <summary>Ctor (no filter).</summary>
    /// <param name="logger">Logger.</param>
    /// <param name="onLog">Callback handler.</param>
    public LogInjector(ILogger logger, Action<ILogEntry> onLog) : this(logger, onLog, null) { }

    /// <summary>Ctor with filter.</summary>
    /// <param name="logger">Logger.</param>
    /// <param name="onLog">Callback.</param>
    /// <param name="filter">Entry predicate.</param>
    public LogInjector(ILogger logger, Action<ILogEntry> onLog, Func<ILogEntry, bool>? filter) {
        _onLog = onLog ?? throw new Exception(nameof(onLog));
        _logger = logger ?? throw new Exception(nameof(logger));
        _filter = filter;
        _logger.AddInjector(ID, this);
    }

    /// <summary>Handle log entry.</summary>
    /// <param name="entry">Entry object.</param>
    internal void Handle(ILogEntry entry) {
        if (_filter == null || _filter(entry)) {
            _onLog(entry);
        }
    }

    /// <summary>Detach injector.</summary>
    public void Dispose() {
        try {
            _logger.RemoveInjector(ID);
        } catch {
            // Swallow exceptions on dispose to avoid tearing down due to race conditions
            // (e.g., injector already removed or logger shutting down).
        }
    }
}
