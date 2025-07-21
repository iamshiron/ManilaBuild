
using Shiron.Manila.Logging;

namespace Shiron.Manila.Caching;

public class LogCache(ILogger logger) {
    private readonly ILogger _logger = logger;
    public readonly List<ILogEntry> Entries = [];

    public void Replay(Guid contextID) {
        foreach (var entry in Entries) {
            _logger.Log(new ReplayLogEntry(entry, contextID));
        }
    }
}
