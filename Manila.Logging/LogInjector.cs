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
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LogInjector"/> class.
    /// </summary>
    /// <param name="onLog">The action to execute for each new log entry. Cannot be null.</param>
    public LogInjector(ILogger logger, Action<ILogEntry> onLog) {
        _onLog = onLog ?? throw new Exception(nameof(onLog));
        _logger = logger ?? throw new Exception(nameof(logger));
        _logger.AddInjector(ID, this);
    }

    /// <summary>
    /// Unregisters this injector from the logger, stopping it from receiving future log entries.
    /// </summary>
    public void Dispose() {
        _logger.RemoveInjector(ID);
    }
}
