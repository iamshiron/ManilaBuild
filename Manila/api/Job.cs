using Microsoft.ClearScript;
using Newtonsoft.Json;
using Shiron.Manila.API.Builders;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Logging;
using Shiron.Manila.Registries;
using Shiron.Manila.Utils;

namespace Shiron.Manila.API;

public interface IJobAction {
    Task ExecuteAsync();
}

public class JobScriptAction(ScriptObject obj) : IJobAction {
    private readonly ScriptObject _scriptObject = obj;
    public string Type => this.GetType().FullName ?? "UnknownJobScriptActionType";

    public async Task ExecuteAsync() {
        try {
            var res = _scriptObject.InvokeAsFunction();
            if (res is Task task) {
                await task;
            }
        } catch (Exception e) {
            throw new ManilaException("Error executing job script action", e);
        }
    }
}
public class JobShellAction(ShellUtils.CommandInfo info) : IJobAction {
    private readonly ShellUtils.CommandInfo _commandInfo = info;
    public string Type => this.GetType().FullName ?? "UnknownJobScriptActionType";

    public async Task ExecuteAsync() {
        ShellUtils.Run(_commandInfo);
        await Task.Yield();
    }
}
public class PrintAction(ILogger logger, string message, string scriptPath, Guid scriptContextID) : IJobAction {
    private readonly ILogger _logger = logger;
    private readonly string _message = message;
    private readonly string _scriptPath = scriptPath;
    private readonly Guid _scriptContextID = scriptContextID;
    public string Type => this.GetType().FullName ?? "UnknownJobScriptActionType";

    public async Task ExecuteAsync() {
        _logger.Log(new ScriptLogEntry(_scriptPath, _message, _scriptContextID));
        await Task.Yield();
    }
}

/// <summary>
/// Represents a job in the build script.
/// </summary>
public class Job(ILogger logger, IJobRegistry jobRegistry, JobBuilder builder) : ExecutableObject {
    private readonly ILogger _logger = logger;
    private readonly IJobRegistry _jobRegistry = jobRegistry;

    public readonly string Name = builder.Name;
    public readonly List<string> Dependencies = builder.Dependencies;
    public readonly IJobAction[] Actions = builder.Actions;
    [JsonIgnore]
    public readonly ScriptContext Context = builder.ScriptContext;
    [JsonIgnore]
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
            Job? dependentJob = _jobRegistry.GetJob(dependency);
            if (dependentJob == null) { _logger.Warning("Job not found: " + dependency); continue; }
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

    protected override async Task RunAsync() {
        _logger.Debug($"Executing job: {ExecutableID}");

        _logger.Log(new JobExecutionStartedLogEntry(this, ExecutableID));
        using (_logger.LogContext.PushContext(ExecutableID)) {
            try {
                foreach (var a in Actions) await a.ExecuteAsync();
            } catch (Exception e) {
                _logger.Log(new JobExecutionFailedLogEntry(this, ExecutableID, e));
                throw;
            }
            _logger.Log(new JobExecutionFinishedLogEntry(this, ExecutableID));
        }
    }
    public override string GetID() {
        return GetIdentifier();
    }

    public override string ToString() {
        return $"Job({GetIdentifier()})";
    }
}
