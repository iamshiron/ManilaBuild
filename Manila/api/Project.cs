using Microsoft.ClearScript;
using Shiron.Manila.Attributes;
using Shiron.Manila.Utils;

namespace Shiron.Manila.API;

/// <summary>
/// Represents a project in the build script.
/// </summary>
public class Project : Component {
    [ScriptProperty(true)]
    public string Name { get; private set; }

    [ScriptProperty]
    public string? Version { get; set; }
    [ScriptProperty]
    public string? Group { get; set; }
    [ScriptProperty]
    public string? Description { get; set; }

    public List<Artifact> Artifacts { get; } = [];

    public Dictionary<string, SourceSet> _sourceSets = [];
    public Dictionary<string, Artifact> _artifacs = [];
    public List<Dependency> _dependencies = [];

    public Workspace Workspace { get; private set; }

    [ScriptFunction]
    public void sourceSets(object obj) {
        foreach (var pair in (IDictionary<string, object>) obj) {
            if (_sourceSets.ContainsKey(pair.Key)) throw new Exception($"SourceSet '{pair.Key}' already exists.");
            _sourceSets.Add(pair.Key, (SourceSet) pair.Value);
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
            if (_artifacs.ContainsKey(pair.Key)) throw new Exception($"Artifact '{pair.Key}' already exists.");
            _artifacs.Add(pair.Key, ((ArtifactBuilder) pair.Value).Build());
        }
    }

    public Project(string name, string location, Workspace workspace) : base(location) {
        this.Name = name;
        this.Workspace = workspace;
    }

    public override string ToString() {
        return $"Project({GetIdentifier()})";
    }

    public override void Finalize(Manila manilaAPI) {
        base.Finalize(manilaAPI);

        Artifacts.AddRange(manilaAPI.ArtifactBuilders.Select(b => b.Build()));
    }
}
