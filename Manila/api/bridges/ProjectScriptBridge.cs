
using Microsoft.ClearScript;
using Shiron.Manila.API;
using Shiron.Manila.API.Builders;
using Shiron.Manila.Logging;
using Shiron.Manila.Profiling;

namespace Shiron.Manila.API.Bridges;

public class ProjectScriptBridge(ILogger logger, IProfiler profiler, Project project) : ScriptBridge {
    private readonly ILogger _logger = logger;
    private readonly IProfiler _profiler = profiler;
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
            if (_handle.SourceSets.ContainsKey(pair.Key)) throw new Exception($"SourceSet '{pair.Key}' already exists.");
            _handle.SourceSetBuilders.Add(pair.Key, (SourceSetBuilder) pair.Value);
        }
    }

    /// <summary>
    /// Adds dependencies to the project from a script object.
    /// </summary>
    /// <param name="obj">Script object containing dependency definitions.</param>
    public void Dependencies(ScriptObject obj) {
        foreach (var n in obj.PropertyIndices) {
            if (obj[n] is Dependency dep) {
                _handle.Dependencies.Add(dep);
            } else {
                throw new InvalidCastException($"Property '{n}' is not a Dependency.");
            }
        }
    }

    /// <summary>
    /// Configures artifacts for the project from a collection of builders.
    /// </summary>
    /// <param name="obj">Dictionary containing artifact names and their builders.</param>
    public void Artifacts(object obj) {
        foreach (var pair in (IDictionary<string, object>) obj) {
            if (_handle.ArtifactBuilders.ContainsKey(pair.Key)) throw new Exception($"Artifact '{pair.Key}' already exists.");
            var builder = (ArtifactBuilder) pair.Value;
            builder.Name = pair.Key;
            _handle.ArtifactBuilders[pair.Key] = builder;
        }
    }
}
