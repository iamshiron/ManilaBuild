
using Microsoft.ClearScript;
using Shiron.Manila.API.Builders;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Logging;
using Shiron.Manila.Utils;

namespace Shiron.Manila.API;

public interface ITaskAction {
    void Execute();
}

public class TaskScriptAction(ScriptObject obj) : ITaskAction {
    private readonly ScriptObject _scriptObject = obj;

    public void Execute() {
        try {
            var res = _scriptObject.InvokeAsFunction();
            if (res is System.Threading.Tasks.Task task) {
                task.Wait();
            }
        } catch (Exception e) {
            Logger.Error("Error executing task script action: " + e.Message);
            throw new ManilaException("Error executing task script action", e);
        }
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
    public readonly string? ArtiafactName = builder.ArtifactBuilder?.Name;
    public readonly string TaskID = Guid.NewGuid().ToString();

    /// <summary>
    /// Get the identifier of the task.
    /// </summary>
    /// <returns>The unique identifier of the task</returns>
    public string GetIdentifier() {
        return new RegexUtils.TaskMatch(Component is Workspace ? null : Component.GetIdentifier(), ArtiafactName, Name).Format();
    }

    /// <summary>
    /// Gets the execution order of the task and its dependencies.
    /// </summary>
    /// <returns>A ascending list of the task execution order</returns>
    public List<string> GetExecutionOrder() {
        List<string> result = [];
        foreach (string dependency in Dependencies) {
            Task? dependentTask = ManilaEngine.GetInstance().GetTask(dependency);
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
