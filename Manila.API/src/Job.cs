using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ClearScript;
using Newtonsoft.Json;
using Shiron.Manila.API.Builders;
using Shiron.Manila.API.Exceptions;
using Shiron.Manila.API.Interfaces;
using Shiron.Manila.API.Logging;
using Shiron.Manila.API.Utils;
using Shiron.Manila.Logging;
using Shiron.Manila.Utils;

namespace Shiron.Manila.API;

/// <summary>
/// Defines a single, executable step within a <see cref="Job"/>.
/// </summary>
public interface IJobAction {
    /// <summary>
    /// Executes the action asynchronously.
    /// </summary>
    Task ExecuteAsync();
}

/// <summary>
/// An action that executes a JavaScript function provided as a <see cref="ScriptObject"/>.
/// </summary>
/// <param name="scriptObject">The script object representing the function to invoke.</param>
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
            // Wrap script engine errors in a more specific exception type.
            throw new ScriptExecutionException("A script error occurred during job execution.", e);
        }
    }
}

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
            // Wrap script engine errors in a more specific exception type.
            throw new ScriptExecutionException("A script error occurred during job execution.", e);
        }
    }
}

/// <summary>
/// An action that executes a command in the system's shell.
/// </summary>
/// <param name="commandInfo">The details of the command to execute.</param>
public class JobShellAction(ILogger logger, ShellUtils.CommandInfo commandInfo) : IJobAction {
    private readonly ILogger _logger = logger;
    private readonly ShellUtils.CommandInfo _commandInfo = commandInfo;

    /// <inheritdoc/>
    public async Task ExecuteAsync() {
        try {
            // Run the command on a thread pool thread and await completion to ensure proper ordering.
            var exitCode = await Task.Run(() => ShellUtils.Run(_commandInfo, _logger));
            if (exitCode != 0) {
                throw new BuildProcessException($"Shell command failed with exit code {exitCode}: '{_commandInfo.Command}'");
            }
        } catch (Exception e) {
            // Wrap any process execution errors in a BuildProcessException.
            throw new BuildProcessException($"Shell command failed: '{_commandInfo.Command}'", e);
        }
    }
}

/// <summary>
/// An action that logs a message using the Manila logging system.
/// </summary>
/// <param name="logger">The logger instance.</param>
/// <param name="message">The message to log.</param>
/// <param name="scriptPath">The path of the script that generated this action.</param>
/// <param name="scriptContextId">The context ID of the script execution.</param>
public class PrintAction(ILogger logger, string message, string scriptPath, Guid scriptContextId) : IJobAction {
    private readonly ILogger _logger = logger;
    private readonly string _message = message;
    private readonly string _scriptPath = scriptPath;
    private readonly Guid _scriptContextId = scriptContextId;

    /// <inheritdoc/>
    public Task ExecuteAsync() {
        // Logging is a fast, synchronous operation; no need for a real async task.
        _logger.Log(new ScriptLogEntry(_scriptPath, _message, _scriptContextId));
        return Task.CompletedTask;
    }
}

/// <summary>
/// Represents an executable unit of work with defined dependencies and actions.
/// </summary>
public class Job(ILogger logger, IJobRegistry jobRegistry, JobBuilder builder) : ExecutableObject {
    private readonly IJobRegistry _jobRegistry = jobRegistry;
    private readonly ILogger _logger = logger;

    /// <summary>Gets the simple name of the job.</summary>
    public readonly string Name = builder.Name;

    /// <summary>Gets the list of identifiers for jobs that must be completed before this one.</summary>
    public readonly List<string> Dependencies = builder.Dependencies;

    /// <summary>Gets the sequence of actions this job will perform when executed.</summary>
    public readonly IJobAction[] Actions = builder.Actions;

    /// <summary>Gets the script context in which this job was defined.</summary>
    [JsonIgnore]
    public readonly IScriptContext Context = builder.ScriptContext;

    /// <summary>Gets the component (e.g., Project or Workspace) this job belongs to.</summary>
    [JsonIgnore]
    public readonly Component Component = builder.Component;

    /// <summary>Gets the user-provided description of the job.</summary>
    public readonly string Description = builder.JobDescription;

    /// <summary>Gets a value indicating whether this job must run serially.</summary>
    public readonly bool Blocking = builder.Blocking;

    /// <summary>Gets the name of the artifact this job contributes to, if any.</summary>
    public readonly string? ArtifactName = builder.ArtifactBuilder?.Name;

    /// <summary>Gets the unique runtime ID for an instance of this job execution.</summary>
    public readonly string JobID = Guid.NewGuid().ToString();

    /// <summary>
    /// Generates the canonical, unique identifier for the job.
    /// </summary>
    /// <returns>A unique string identifier for the job definition.</returns>
    public string GetIdentifier() {
        var componentId = (Component is Workspace) ? null : Component.GetIdentifier();
        return new RegexUtils.JobMatch(componentId, ArtifactName, Name).Format();
    }

    /// <inheritdoc/>
    public override bool IsBlocking() {
        return Blocking;
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public override string GetID() {
        return GetIdentifier();
    }

    /// <inheritdoc/>
    public override string ToString() {
        return $"Job({GetIdentifier()})";
    }
}
