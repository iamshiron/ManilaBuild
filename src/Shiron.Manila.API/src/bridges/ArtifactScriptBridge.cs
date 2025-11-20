
using Microsoft.ClearScript;
using Shiron.Logging;
using Shiron.Manila.API.Builders;
using Shiron.Manila.API.Interfaces;
using Shiron.Manila.API.Interfaces.Artifacts;
using Shiron.Manila.API.Utils;
using Shiron.Manila.Exceptions;

namespace Shiron.Manila.API.Bridges;

/// <summary>
/// Represents a bridge for unresolved artifact scripts.
/// </summary>
/// <param name="project">The parent project.</param>
/// <param name="builder">The artifact builder.</param>
/// <param name="artifactID">The unique identifier for the artifact.</param>
/// <param name="pluginComponent">The plugin component match information.</param>
public class UnresolvedArtifactScriptBridge(Project project, ArtifactBuilder builder, string artifactID, RegexUtils.PluginComponentMatch pluginComponent) : ScriptBridge {
    public readonly string ArtifactID = artifactID;
    public readonly RegexUtils.PluginComponentMatch PluginComponent = pluginComponent;
    private readonly Project _parentProject = project;

    /// <summary>
    /// Sets the description for the artifact.
    /// </summary>
    /// <param name="description">The description text.</param>
    public void Description(string description) {
        builder.Description = description;
    }
    /// <summary>
    /// Sets the dependencies for the artifact.
    /// </summary>
    /// <param name="obj">The script object containing dependencies within an array.</param>
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

    /// <summary>
    /// Resolves the unresolved artifact to a concrete created artifact.
    /// </summary>
    /// <returns>The resolved created artifact.</returns>
    /// <exception cref="ManilaException">Thrown if the artifact cannot be resolved.</exception>
    public ICreatedArtifact Resolve() {
        return _parentProject.Artifacts.TryGetValue(ArtifactID, out var artifact)
            ? artifact
            : throw new ManilaException($"Artifact '{ArtifactID}' could not be resolved in project '{_parentProject.Name}'!");
    }
}
