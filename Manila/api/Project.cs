using Microsoft.ClearScript;
using NuGet.Packaging;
using Shiron.Manila.Artifacts;
using Shiron.Manila.Attributes;
using Shiron.Manila.Logging;
using Shiron.Manila.Utils;

namespace Shiron.Manila.API;

/// <summary>
/// Represents a project in the build script.
/// </summary>
public class Project(string name, string location, Workspace workspace) : Component(location) {
    [ScriptProperty(true)]
    public string Name { get; private set; } = name;

    [ScriptProperty]
    public string? Version { get; set; }
    [ScriptProperty]
    public string? Group { get; set; }
    [ScriptProperty]
    public string? Description { get; set; }

    public Dictionary<string, Artifact> Artifacts { get; } = [];
    public Dictionary<string, SourceSet> SourceSets = [];

    private readonly Dictionary<string, ArtifactBuilder> _artifactBuilders = [];
    private readonly Dictionary<string, SourceSetBuilder> _sourceSetBuilders = [];

    public List<Dependency> _dependencies = [];

    public Workspace Workspace { get; private set; } = workspace;

    [ScriptFunction]
    public void sourceSets(object obj) {
        foreach (var pair in (IDictionary<string, object>) obj) {
            if (SourceSets.ContainsKey(pair.Key)) throw new Exception($"SourceSet '{pair.Key}' already exists.");
            _sourceSetBuilders.Add(pair.Key, (SourceSetBuilder) pair.Value);
        }
    }

    [ScriptFunction]
    public void dependencies(object obj) {
        ScriptObject sobj = (ScriptObject) obj;
        foreach (var n in sobj.PropertyIndices) {
            if (sobj[n] is Dependency dep) {
                _dependencies.Add(dep);
            } else {
                throw new InvalidCastException($"Property '{n}' is not a Dependency.");
            }
        }
    }

    [ScriptFunction]
    public void artifacts(object obj) {
        foreach (var pair in (IDictionary<string, object>) obj) {
            if (_artifactBuilders.ContainsKey(pair.Key)) throw new Exception($"Artifact '{pair.Key}' already exists.");
            var builder = (ArtifactBuilder) pair.Value;
            builder.Name = pair.Key;
            _artifactBuilders[pair.Key] = builder;
        }
    }

    public override string ToString() {
        return $"Project({GetIdentifier()})";
    }

    public override void Finalize(Manila manilaAPI) {
        base.Finalize(manilaAPI);

        foreach (var (name, builder) in _artifactBuilders) {
            Artifacts[name] = builder.Build();
        }
        foreach (var (name, builder) in _sourceSetBuilders) {
            SourceSets[name] = builder.Build();
            Logger.Debug($"{Name} - {name} - SHA256: {SourceSets[name].Fingerprint()}");
        }
    }
}
