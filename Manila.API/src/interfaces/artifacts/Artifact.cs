
using Shiron.Manila.API.Utils;
using Shiron.Manila.Utils;

namespace Shiron.Manila.API.Interfaces.Artifacts;

public interface IArtifact {
    string Description { get; }
    Job[] Jobs { get; }
    string Name { get; }
    UnresolvedProject Project { get; }
    RegexUtils.PluginComponentMatch PluginComponent { get; }

    LogCache? LogCache { get; set; }

    string GetFingerprint(BuildConfig config);
}
