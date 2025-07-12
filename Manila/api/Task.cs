
using Shiron.Manila.Exceptions;
using Shiron.Manila.Logging;
using Shiron.Manila.Utils;
using Spectre.Console;

namespace Shiron.Manila.API;

public interface ITaskAction {
    void Execute();
}

public class TaskScriptAction(dynamic action) : ITaskAction {
    private readonly Action _action = () => action();

    public void Execute() {
        _action.Invoke();
    }
}
public class TaskShellAction(ShellUtils.CommandInfo info) : ITaskAction {
    private readonly ShellUtils.CommandInfo _commandInfo = info;

    public void Execute() {
        ShellUtils.Run(_commandInfo);
    }
}
public class PrintAction(string message, string scriptPath, Guid scriptContextID) : ITaskAction {
    private readonly string _message = message;
    private readonly string _scriptPath = scriptPath;
    private readonly Guid _scriptContextID = scriptContextID;

    public void Execute() {
        Logger.Log(new ScriptLogEntry(_scriptPath, _message, _scriptContextID));
    }
}

public sealed class TaskBuilder(string name, ScriptContext context, Component component) : IBuildable<Task> {
    public readonly string Name = name;
    public string Description { get; private set; } = "A generic task";
    public bool Blocking { get; private set; } = true;

    public readonly List<string> Dependencies = [];
    public ITaskAction[] Actions { get; private set; } = [];
    public readonly ScriptContext ScriptContext = context;
    public readonly Component Component = component;

    /// <summary>
    /// Add a dependency to the task.
    /// </summary>
    /// <param name="task">The dependents task ID</param>
    /// <returns>Task instance for chaining calls</returns>
    public TaskBuilder after(string task) {
        if (task.StartsWith(":")) {
            Dependencies.Add(task[1..]);
            Logger.Debug($"{this}, added {task[1..]}");
        } else {
            var prefix = Component is Workspace ? "" : $"{Component.GetIdentifier()}:";
            Dependencies.Add($"{prefix}{task}");
        }

        return this;
    }
    /// <summary>
    /// The action to be executed by the task.
    /// </summary>
    /// <param name="action">The action</param>
    /// <returns>Task instance for chaining calls</returns>
    public TaskBuilder execute(object o) {
        if (o is ITaskAction action) {
            Logger.Debug($"Found task action of type {action.GetType().FullName}");
            Actions = [action];
        } else
        if (o is IList<object> list) {
            Logger.Debug($"Found {list.Count} chained actions!");
            Actions = list.Cast<ITaskAction>().ToArray();
        } else {
            Actions = [new TaskScriptAction((dynamic) o)];
        }

        return this;
    }
    /// <summary>
    /// Set the description of the task.
    /// </summary>
    /// <param name="description">The description</param>
    /// <returns>Task instance for chaining calls</returns>
    public TaskBuilder description(string description) {
        this.Description = description;
        return this;
    }
    /// <summary>
    /// Sets a task's blocking mode, meaning if it will block the execution flow or is running in the background
    /// </summary>
    /// <param name="background">True: Non Blocking, False: Blocking</param>
    /// <returns></returns>
    public TaskBuilder background(bool background = true) {
        this.Blocking = !background;
        return this;
    }

    public Task Build() {
        return new(this);
    }
}

/// <summary>
/// Represents a task in the build script.
/// </summary>
public class Task(TaskBuilder builder) : ExecutableObject {
    public readonly string Name = builder.Name;
    public readonly List<string> Dependencies = builder.Dependencies;
    public readonly ITaskAction[] Actions = builder.Actions;
    public readonly ScriptContext Context = builder.ScriptContext;
    public readonly Component Component = builder.Component;
    public readonly string Description = builder.Description;
    public readonly bool Blocking = builder.Blocking;
    public readonly string TaskID = Guid.NewGuid().ToString();

    /// <summary>
    /// Get the identifier of the task.
    /// </summary>
    /// <returns>The unique identifier of the task</returns>
    public string GetIdentifier() {
        if (Component is Project) return $"{Component.GetIdentifier()}:{Name}";
        return Name;

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
        Logger.Log(new TaskExecutionStartedLogEntry(this, ExecutableID));
        using (LogContext.PushContext(ExecutableID)) {
            try {
                foreach (var a in Actions) a.Execute();
            } catch (Exception e) {
                Logger.Log(new TaskExecutionFailedLogEntry(this, ExecutableID, e));
                throw;
            }
            Logger.Log(new TaskExecutionFinishedLogEntry(this, ExecutableID));
        }
    }
    public override string GetID() {
        return GetIdentifier();
    }

    public override string ToString() {
        return $"Task({GetIdentifier()})";
    }
}
