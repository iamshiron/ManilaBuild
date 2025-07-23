
using Shiron.Manila.Artifacts;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Logging;
using Shiron.Manila.Profiling;

namespace Shiron.Manila.API.Bridges;

public class UnresolvedArtifactScriptBridge(Project project, string artifactID) : ScriptBridge {
    private readonly string _artifactID = artifactID;
    private readonly Project _parentProject = project;

    public Artifact Resolve() {
        return _parentProject.Artifacts.TryGetValue(_artifactID, out var artifact)
            ? artifact
            : throw new ManilaException($"Artifact '{_artifactID}' could not be resolved in project '{_parentProject.Name}'!");
    }
}
