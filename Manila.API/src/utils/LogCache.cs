

using Shiron.Manila.API.Interfaces;
using Shiron.Manila.API.Logging;
using Shiron.Manila.Logging;

namespace Shiron.Manila.API.Utils;

public class LogCache {
    public readonly List<ILogEntry> Entries = [];

    public void Replay(ILogger logger, Guid contextID) {
        foreach (var entry in Entries) {
            logger.Log(new ReplayLogEntry(entry, contextID));
        }
    }
}
