
using Shiron.Manila.API.Interfaces.Artifacts;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Logging;
using Shiron.Manila.Profiling;
using Shiron.Manila.Utils;

namespace Shiron.Manila.API.Bridges;

public class UnresolvedArtifactScriptBridge(Project project, string artifactID, RegexUtils.PluginComponentMatch pluginComponent) : ScriptBridge {
    public readonly string ArtifactID = artifactID;
    public readonly RegexUtils.PluginComponentMatch PluginComponent = pluginComponent;
    private readonly Project _parentProject = project;

    public IArtifact Resolve() {
        return _parentProject.Artifacts.TryGetValue(ArtifactID, out var artifact)
            ? artifact
            : throw new ManilaException($"Artifact '{ArtifactID}' could not be resolved in project '{_parentProject.Name}'!");
    }
}
