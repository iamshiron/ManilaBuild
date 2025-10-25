
using Microsoft.ClearScript;
using Shiron.Manila.API.Builders;
using Shiron.Manila.API.Exceptions;
using Shiron.Manila.API.Interfaces;
using Shiron.Manila.API.Interfaces.Artifacts;
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
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var i in (IList<object>) obj) {
            if (i is not IDependency) {
                throw new ConfigurationException($"Invalid dependency type '{i.GetType().Name}' in artifact '{ArtifactID}' of project '{_parentProject.Name}'!");
            }

            var dep = (IDependency) i;
            // Use the type+string form where available to dedupe; fall back to type name
            var key = dep switch {
                Shiron.Manila.API.Dependencies.ArtifactDependency ad => $"Artifact:{ad.GetHashCode()}", // GetHashCode isn't ideal; real impl doesn't expose fields
                _ => dep.GetType().FullName ?? dep.GetType().Name
            };
            if (seen.Add(key)) builder.Dependencies.Add(dep);
        }
    }

    public ICreatedArtifact Resolve() {
        return _parentProject.Artifacts.TryGetValue(ArtifactID, out var artifact)
            ? artifact
            : throw new ManilaException($"Artifact '{ArtifactID}' could not be resolved in project '{_parentProject.Name}'!");
    }
}
