using Shiron.Manila.Logging;

namespace Shiron.Manila.API;

/// <summary>
/// Represents a task in the build script.
/// </summary>
public class Task {
    public readonly string Name;
    public readonly List<string> dependencies = [];
    public Action? Action { get; private set; }
    private readonly ScriptContext _context;
    public Component Component { get; init; }
    public string ScriptPath { get; init; }
    public string Description { get; set; } = "A generic task";

    /// <summary>
    /// Get the identifier of the task.
    /// </summary>
    /// <returns>The unique identifier of the task</returns>
    public string GetIdentifier() {
        return $"{Component.GetIdentifier()}:{Name}";
    }

    public Task(string name, Component component, ScriptContext context, string scriptPath) {
        if (name.Contains(":")) throw new Exception("Task name cannot contain a colon (:) character.");

        this.Name = name;
        this.Component = component;
        this._context = context;
        this.Component.Tasks.Add(this);
        this.ScriptPath = scriptPath;

        Logger.Log(new TaskDiscoveredLogEntry(this, Component));
    }

    /// <summary>
    /// Add a dependency to the task.
    /// </summary>
    /// <param name="task">The dependents task ID</param>
    /// <returns>Task instance for chaining calls</returns>
    public Task after(string task) {
        if (task.StartsWith(":")) {
            dependencies.Add(task[1..]);
        } else {
            dependencies.Add($"{Component.GetIdentifier()}:{task}");
        }

        return this;
    }
    /// <summary>
    /// The action to be executed by the task.
    /// </summary>
    /// <param name="action">The action</param>
    /// <returns>Task instance for chaining calls</returns>
    public Task execute(dynamic action) {
        this.Action = () => {
            try {
                action();
            } catch (Exception e) {
                Logger.Error("Task failed: " + Name);
                Logger.Error(e.GetType().Name + ": " + e.Message);
                throw;
            }
        };
        return this;
    }
    /// <summary>
    /// Set the description of the task.
    /// </summary>
    /// <param name="description">The description</param>
    /// <returns>Task instance for chaining calls</returns>
    public Task description(string description) {
        this.Description = description;
        return this;
    }

    /// <summary>
    /// Gets the execution order of the task and its dependencies.
    /// </summary>
    /// <returns>A ascending list of the task execution order</returns>
    public List<string> GetExecutionOrder() {
        List<string> result = [];
        foreach (string dependency in dependencies) {
            Task? dependentTask = ManilaEngine.GetInstance().Workspace.GetTask(dependency);
            if (dependentTask == null) { Logger.Warning("Task not found: " + dependency); continue; }
            List<string> dependencyOrder = dependentTask.GetExecutionOrder();
            foreach (string depTask in dependencyOrder) {
                if (!result.Contains(depTask)) {
                    result.Add(depTask);
                }
            }

        }
        if (!result.Contains(GetIdentifier())) {
            result.Add(GetIdentifier());
        }

        return result;
    }
}
