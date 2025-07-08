using Shiron.Manila.Exceptions;
using Shiron.Manila.Logging;
using Shiron.Manila.Utils;

namespace Shiron.Manila.API;

/// <summary>
/// Represents a workspace containing projects.
/// </summary>
public class Workspace : Component {
    public Dictionary<string, Project> Projects { get; } = new();
    public List<Tuple<ProjectFilter, Action<Project>>> ProjectFilters { get; } = new();

    public Workspace(string location) : base(location) {
    }

    /// <summary>
    /// Gets the task inside the workspace.
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public Task GetTask(string key) {
        if (key.StartsWith(":")) return GetTask(this, key[1..]);
        var parts = key.Split(":");
        return GetTask(Projects[parts[0]], parts[1]);
    }
    public Task GetTask(Component component, string task) {
        return component.Tasks.FirstOrDefault(t => t.Name == task) ?? throw new TaskNotFoundException(task);
    }

    public bool HasTask(string key) {
        if (key.StartsWith(":")) return HasTask(this, key[1..]);
        var parts = key.Split(":");
        return HasTask(Projects[parts[0]], parts[1]);
    }
    public bool HasTask(Component component, string key) {
        Console.WriteLine($"{component.GetType().FullName}, {key}");
        return component.Tasks.FirstOrDefault(t => t.Name == key) != null;
    }

    public override string GetIdentifier() {
        return "";
    }

    public override string ToString() {
        return $"Workspace()";
    }
}
