
using Microsoft.ClearScript;
using Shiron.Manila.API.Builders;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Logging;
using Shiron.Manila.Utils;

namespace Shiron.Manila.API;

public interface IJobAction {
    Task Execute();
}

public class JobScriptAction(ScriptObject obj) : IJobAction {
    private readonly ScriptObject _scriptObject = obj;

    public async Task Execute() {
        try {
            var res = _scriptObject.InvokeAsFunction();
            if (res is Task task) {
                await task;
            }
        } catch (Exception e) {
            Logger.Error("Error executing job script action: " + e.Message);
            throw new ManilaException("Error executing job script action", e);
        }
    }
}
public class JobShellAction(ShellUtils.CommandInfo info) : IJobAction {
    private readonly ShellUtils.CommandInfo _commandInfo = info;

    public async Task Execute() {
        ShellUtils.Run(_commandInfo);
        await Task.Yield();
    }
}
public class PrintAction(string message, string scriptPath, Guid scriptContextID) : IJobAction {
    private readonly string _message = message;
    private readonly string _scriptPath = scriptPath;
    private readonly Guid _scriptContextID = scriptContextID;

    public async Task Execute() {
        Logger.Log(new ScriptLogEntry(_scriptPath, _message, _scriptContextID));
        await Task.Yield();
    }
}

/// <summary>
/// Represents a job in the build script.
/// </summary>
public class Job(JobBuilder builder) : ExecutableObject {
    public readonly string Name = builder.Name;
    public readonly List<string> Dependencies = builder.Dependencies;
    public readonly IJobAction[] Actions = builder.Actions;
    public readonly ScriptContext Context = builder.ScriptContext;
    public readonly Component Component = builder.Component;
    public readonly string Description = builder.Description;
    public readonly bool Blocking = builder.Blocking;
    public readonly string? ArtiafactName = builder.ArtifactBuilder?.Name;
    public readonly string JobID = Guid.NewGuid().ToString();

    /// <summary>
    /// Get the identifier of the job.
    /// </summary>
    /// <returns>The unique identifier of the job</returns>
    public string GetIdentifier() {
        return new RegexUtils.JobMatch(Component is Workspace ? null : Component.GetIdentifier(), ArtiafactName, Name).Format();
    }

    /// <summary>
    /// Gets the execution order of the job and its dependencies.
    /// </summary>
    /// <returns>A ascending list of the job execution order</returns>
    public List<string> GetExecutionOrder() {
        List<string> result = [];
        foreach (string dependency in Dependencies) {
            Job? dependentJob = ManilaEngine.GetInstance().GetJob(dependency);
            if (dependentJob == null) { Logger.Warning("Job not found: " + dependency); continue; }
            List<string> dependencyOrder = dependentJob.GetExecutionOrder();
            foreach (string depJob in dependencyOrder) {
                if (!result.Contains(depJob)) {
                    result.Add(depJob);
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

    protected override async Task Run() {
        Logger.Log(new JobExecutionStartedLogEntry(this, ExecutableID));
        using (LogContext.PushContext(ExecutableID)) {
            try {
                foreach (var a in Actions) await a.Execute();
            } catch (Exception e) {
                Logger.Log(new JobExecutionFailedLogEntry(this, ExecutableID, e));
                throw;
            }
            Logger.Log(new JobExecutionFinishedLogEntry(this, ExecutableID));
        }
    }
    public override string GetID() {
        return GetIdentifier();
    }

    public override string ToString() {
        return $"Job({GetIdentifier()})";
    }
}
