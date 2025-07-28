
using Shiron.Manila.API;
using Shiron.Manila.API.Builders;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Logging;
using Shiron.Manila.Profiling;

namespace Shiron.Manila.API.Bridges;

public class ProjectScriptBridge(Project project) : ScriptBridge {
    internal readonly Project _handle = project;

    public void Version(string version) {
        _handle.Version = version;
    }
    public void Description(string description) {
        _handle.Description = description;
    }

    public DirHandle GetPath() {
        return new(_handle.Path);
    }
    public string GetName() {
        return _handle.Name;
    }

    /// <summary>
    /// Configures source sets for the project from a collection of builders.
    /// </summary>
    /// <param name="obj">Dictionary containing source set names and their builders.</param>
    public void SourceSets(object obj) {
        foreach (var pair in (IDictionary<string, object>) obj) {
            if (_handle.SourceSets.ContainsKey(pair.Key)) throw new ManilaException($"SourceSet '{pair.Key}' already exists.");
            _handle.SourceSetBuilders.Add(pair.Key, (SourceSetBuilder) pair.Value);
        }
    }

    /// <summary>
    /// Configures artifacts for the project from a collection of builders.
    /// </summary>
    /// <param name="obj">Dictionary containing artifact names and their builders.</param>
    public void Artifacts(object obj) {
        foreach (var pair in (IDictionary<string, object>) obj) {
            if (_handle.ArtifactBuilders.ContainsKey(pair.Key)) throw new ManilaException($"Artifact '{pair.Key}' already exists.");
            var builder = (ArtifactBuilder) pair.Value;
            builder.Name = pair.Key;
            _handle.ArtifactBuilders[pair.Key] = builder;
        }
    }
}
