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

    public Dictionary<string, SourceSet> _sourceSets = [];
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
            _dependencies.Add(sobj[n] as Dependency);
        }
    }

    public Project(string name, string location, Workspace workspace) : base(location) {
        this.Name = name;
        this.Workspace = workspace;
    }
}
