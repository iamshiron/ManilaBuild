using Shiron.Manila.Attributes;

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

    public Dictionary<string, SourceSet> _sourceSets = new();

    [ScriptFunction]
    public void sourceSets(object obj) {
        foreach (var pair in (IDictionary<string, object>) obj) {
            if (_sourceSets.ContainsKey(pair.Key)) throw new Exception($"SourceSet '{pair.Key}' already exists.");
            _sourceSets.Add(pair.Key, (SourceSet) pair.Value);
        }
    }

    public Project(string name, string location) : base(location) {
        this.Name = name;
    }
}
