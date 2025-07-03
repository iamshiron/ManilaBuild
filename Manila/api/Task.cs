
using Shiron.Manila.Logging;
using Shiron.Manila.Utils;

namespace Shiron.Manila.API;

/// <summary>
/// Represents a task in the build script.
/// </summary>
public class Task : ExecutableObject {
    public readonly string Name;
    public readonly List<string> Dependencies = [];
    public Action? Action { get; private set; }
    private readonly ScriptContext _context;
    public Component Component { get; init; }
    public string ScriptPath { get; init; }
    public string Description { get; set; } = "A generic task";
    public bool Blocking { get; set; } = true;

    /// <summary>
    /// Get the identifier of the task.
    /// </summary>
    /// <returns>The unique identifier of the task</returns>
    public string GetIdentifier() {
        if (Component is Project) return $"{Component.GetIdentifier()}:{Name}";
        return Name;

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
            Dependencies.Add(task[1..]);
        } else {
            Dependencies.Add($"{Component.GetIdentifier()}:{task}");
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
    /// Sets a task's blocking mode, meaning if it will block the execution flow or is running in the background
    /// </summary>
    /// <param name="background">True: Non Blocking, False: Blocking</param>
    /// <returns></returns>
    public Task background(bool background = true) {
        this.Blocking = !background;
        return this;
    }

    /// <summary>
    /// Gets the execution order of the task and its dependencies.
    /// </summary>
    /// <returns>A ascending list of the task execution order</returns>
    public List<string> GetExecutionOrder() {
        List<string> result = [];
        foreach (string dependency in Dependencies) {
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

    public override bool IsBlocking() {
        return Blocking;
    }

    protected override void Run() {
        var taskContextID = Guid.NewGuid();
        Logger.Log(new TaskExecutionStartedLogEntry(this, taskContextID));
        try {
            Action.Invoke();
        } catch (Exception e) {
            Logger.Log(new TaskExecutionFailedLogEntry(this, taskContextID, e));
            throw;
        }
        Logger.Log(new TaskExecutionFinishedLogEntry(this, taskContextID));
    }
    public override string GetID() {
        return GetIdentifier();
    }

    public override string ToString() {
        return $"Task({GetIdentifier()})";
    }
}
