
using Shiron.Manila.Logging;

namespace Shiron.Manila.Caching;

public class LogCache {
    public readonly List<ILogEntry> Entries = [];

    public void Replay(Guid contextID) {
        foreach (var entry in Entries) {
            Logger.Log(new ReplayLogEntry(entry, contextID));
        }
    }
}

public class ReplayLogEntry(ILogEntry entry, Guid contextID) : ILogEntry {
    public ILogEntry Entry { get; } = entry;
    public Guid ContextID { get; } = contextID;

    /// <inheritdoc />
    public long Timestamp { get; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// <inheritdoc />
    public LogLevel Level => LogLevel.System;

    /// <summary>
    /// Parent context ID for this log entry is always null, use <see cref="ContextID"/> instead.
    /// </summary>
    public virtual Guid? ParentContextID { get; } = null;
}
