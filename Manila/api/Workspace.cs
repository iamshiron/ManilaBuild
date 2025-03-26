namespace Shiron.Manila.API;

/// <summary>
/// Represents a workspace containing projects.
/// </summary>
public class Workspace : Component {
    public Dictionary<string, Project> Projects { get; } = new();
    public List<Tuple<ProjectFilter, Action<Project>>> ProjectFilters { get; } = new();

    public Workspace(string location) : base(location) {
    }

    public Task GetTask(string identifier) {
        if (!identifier.Contains(":")) return GetTask(identifier, null);
        return GetTask(
            identifier[(identifier.LastIndexOf(":") + 1)..],
            identifier[..identifier.LastIndexOf(":")]
        );
    }
    public Task GetTask(string task, string? project = null) {
        if (project == string.Empty) project = null;
        if (project == null) return tasks.First(t => t.name == task);
        if (project.StartsWith(":")) project = project[1..];
        return Projects[project].tasks.First(t => t.name == task);
    }

    public override string GetIdentifier() {
        return "";
    }
}
