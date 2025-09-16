
using Microsoft.ClearScript;
using Shiron.Manila.API.Builders;
using Shiron.Manila.API.Interfaces;
using Shiron.Manila.API.Interfaces.Artifacts;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Logging;
using Shiron.Manila.Profiling;
using Shiron.Manila.Utils;

namespace Shiron.Manila.API.Bridges;

public class UnresolvedArtifactScriptBridge(Project project, ArtifactBuilder builder, string artifactID, RegexUtils.PluginComponentMatch pluginComponent) : ScriptBridge {
    public readonly string ArtifactID = artifactID;
    public readonly RegexUtils.PluginComponentMatch PluginComponent = pluginComponent;
    private readonly Project _parentProject = project;

    public void Description(string description) {
        builder.Description = description;
    }
    public void Dependencies(ScriptObject obj) {
        foreach (var i in (IList<object>) obj) {
            if (i is not IDependency) {
                throw new ConfigurationException($"Invalid dependency type '{i.GetType().Name}' in artifact '{ArtifactID}' of project '{_parentProject.Name}'!");
            }

            builder.Dependencies.Add((IDependency) i);
        }
    }

    public ICreatedArtifact Resolve() {
        return _parentProject.Artifacts.TryGetValue(ArtifactID, out var artifact)
            ? artifact
            : throw new ManilaException($"Artifact '{ArtifactID}' could not be resolved in project '{_parentProject.Name}'!");
    }
}
