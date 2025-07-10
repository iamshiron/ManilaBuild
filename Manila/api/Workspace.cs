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
        Logger.Debug($"GetTask({key})");
        var parts = key.Split(":");
        if (parts.Length > 1) return GetTask(Projects[parts[0]], parts[1]);
        return GetTask(this, key);
    }
    public Task GetTask(Component component, string task) {
        Logger.Debug($"GetTask({component}, {task})");
        return component.Tasks.FirstOrDefault(t => t.Name == task) ?? throw new TaskNotFoundException(task);
    }

    public bool HasTask(string key) {
        var parts = key.Split(":");
        if (parts.Length > 1) return HasTask(Projects[parts[0]], parts[1]);
        return HasTask(this, key);
    }
    public bool HasTask(Component component, string key) {
        Logger.Debug($"HasTask({component.GetType().FullName}, {key})");
        return component.Tasks.FirstOrDefault(t => t.Name == key) != null;
    }

    public override string GetIdentifier() {
        return "";
    }

    public override string ToString() {
        return $"Workspace()";
    }
}
