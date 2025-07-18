namespace Shiron.Manila.Logging;

/// <summary>
/// Represents a log injector that allows custom actions to be performed whenever a log entry is created.
/// </summary>
/// <remarks>
/// This class should be disposed to unregister the injector and prevent memory leaks.
/// </remarks>
public class LogInjector : IDisposable {
    /// <summary>
    /// The callback action to execute when a log entry is created.
    /// </summary>
    internal readonly Action<ILogEntry> _onLog;

    /// <summary>
    /// Gets the unique identifier for this log injector instance.
    /// </summary>
    public readonly Guid ID = Guid.NewGuid();

    /// <summary>
    /// Initializes a new instance of the <see cref="LogInjector"/> class.
    /// </summary>
    /// <param name="onLog">The action to execute for each new log entry. Cannot be null.</param>
    public LogInjector(Action<ILogEntry> onLog) {
        _onLog = onLog ?? throw new ArgumentNullException(nameof(onLog));
        Logger.AddInjector(ID, this);
    }

    /// <summary>
    /// Unregisters this injector from the logger, stopping it from receiving future log entries.
    /// </summary>
    public void Dispose() {
        Logger.RemoveInjector(ID);
    }
}
