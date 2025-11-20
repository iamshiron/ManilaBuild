using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ClearScript;
using Newtonsoft.Json;
using Shiron.Logging;
using Shiron.Manila.API.Builders;
using Shiron.Manila.API.Interfaces;
using Shiron.Manila.API.Logging;
using Shiron.Manila.API.Utils;
using Shiron.Manila.Exceptions;
using Shiron.Utils;

namespace Shiron.Manila.API;

/// <summary>Executable job action step.</summary>
public interface IJobAction {
    /// <summary>Run action.</summary>
    Task ExecuteAsync();
}

/// <summary>Invoke JS function.</summary>
/// <param name="handle">Script function object.</param>
public class JobAsyncScriptAction(ScriptObject handle) : IJobAction {
    private readonly ScriptObject _handle = handle;

    /// <inheritdoc/>
    public async Task ExecuteAsync() {
        try {
            var res = _handle.InvokeAsFunction();
            if (res is Task task) {
                await task;
            }
        } catch (Exception e) {
            throw new ScriptExecutionException("A script error occurred during job execution.", e);
        }
    }
}

/// <summary>Invoke JS function (sync or async).</summary>
/// <param name="handle">Script function object.</param>
public class JobScriptAction(ScriptObject handle) : IJobAction {
    private readonly ScriptObject _handle = handle;

    /// <inheritdoc/>
    public async Task ExecuteAsync() {
        try {
            var res = _handle.InvokeAsFunction();
            if (res is Task task) {
                await task;
            }
        } catch (Exception e) {
            throw new ScriptExecutionException("A script error occurred during job execution.", e);
        }
    }
}

/// <summary>Execute shell command.</summary>
/// <param name="logger">Logger.</param>
/// <param name="commandInfo">Command info.</param>
public class JobShellAction(ILogger logger, ShellUtils.CommandInfo commandInfo) : IJobAction {
    private readonly ILogger _logger = logger;
    private readonly ShellUtils.CommandInfo _commandInfo = commandInfo;

    /// <inheritdoc/>
    public async Task ExecuteAsync() {
        try {
            var exitCode = await Task.Run(() => ShellUtils.Run(_commandInfo, _logger));
            if (exitCode != 0) {
                throw new BuildProcessException($"Shell command failed with exit code {exitCode}: '{_commandInfo.Command}'");
            }
        } catch (Exception e) {
            throw new BuildProcessException($"Shell command failed: '{_commandInfo.Command}'", e);
        }
    }
}

/// <summary>Log script message.</summary>
/// <param name="logger">Logger.</param>
/// <param name="message">Message text.</param>
/// <param name="scriptPath">Script path.</param>
/// <param name="scriptContextId">Context ID.</param>
public class PrintAction(ILogger logger, string message, string scriptPath, Guid scriptContextId) : IJobAction {
    private readonly ILogger _logger = logger;
    private readonly string _message = message;
    private readonly string _scriptPath = scriptPath;
    private readonly Guid _scriptContextId = scriptContextId;

    /// <inheritdoc/>
    public Task ExecuteAsync() {
        _logger.Log(new ScriptLogEntry(_scriptPath, _message, _scriptContextId));
        return Task.CompletedTask;
    }
}

/// <summary>Executable job definition.</summary>
/// <param name="logger">Logger instance.</param>
/// <param name="jobRegistry">Job registry.</param>
/// <param name="builder">Builder data.</param>
public class Job(ILogger logger, IJobRegistry jobRegistry, JobBuilder builder) : ExecutableObject {
    private readonly IJobRegistry _jobRegistry = jobRegistry;
    private readonly ILogger _logger = logger;

    /// <summary>Job name.</summary>
    public readonly string Name = builder.Name;

    /// <summary>Dependency job IDs.</summary>
    public readonly List<string> Dependencies = builder.Dependencies;

    /// <summary>Execution actions.</summary>
    public readonly IJobAction[] Actions = builder.Actions;

    /// <summary>Defining script context.</summary>
    [JsonIgnore]
    public readonly IScriptContext Context = builder.ScriptContext;

    /// <summary>Owning component.</summary>
    [JsonIgnore]
    public readonly Component Component = builder.Component;

    /// <summary>User description.</summary>
    public readonly string Description = builder.JobDescription;

    /// <summary>Blocking flag.</summary>
    public readonly bool Blocking = builder.Blocking;

    /// <summary>Target artifact name (optional).</summary>
    public readonly string? ArtifactName = builder.ArtifactBuilder?.Name;

    /// <summary>Runtime execution ID.</summary>
    public readonly string JobID = Guid.NewGuid().ToString();

    /// <summary>Compute canonical identifier.</summary>
    /// <returns>Identifier string.</returns>
    public string GetIdentifier() {
        var componentId = (Component is Workspace) ? null : Component.GetIdentifier();
        return new RegexUtils.JobMatch(componentId, ArtifactName, Name).Format();
    }

    /// <summary>Blocking check.</summary>
    public override bool IsBlocking() {
        return Blocking;
    }

    /// <summary>Run job actions.</summary>
    public override async Task RunAsync() {
        _logger.Log(new JobExecutionStartedLogEntry(this, ExecutableID));
        using (_logger.LogContext.PushContext(ExecutableID)) {
            try {
                foreach (var action in Actions) {
                    await action.ExecuteAsync().ConfigureAwait(false);
                }
            } catch (Exception e) {
                _logger.Log(new JobExecutionFailedLogEntry(this, ExecutableID, e));
                throw;
            }
            _logger.Log(new JobExecutionFinishedLogEntry(this, ExecutableID));
        }
    }

    /// <summary>Identifier for execution system.</summary>
    public override string GetID() {
        return GetIdentifier();
    }

    /// <summary>Debug string.</summary>
    public override string ToString() {
        return $"Job({GetIdentifier()})";
    }
}
