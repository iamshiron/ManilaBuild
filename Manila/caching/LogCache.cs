
using Shiron.Manila.Logging;

namespace Shiron.Manila.Caching;

public class LogCache {
    public readonly List<ILogEntry> Entries = [];

    public void Replay() {
        foreach (var entry in Entries) Logger.Log(entry);
    }
}
