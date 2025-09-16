using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ClearScript;
using Shiron.Manila.API.Interfaces;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Logging;
using Shiron.Manila.Utils;

namespace Shiron.Manila.API.Builders;

/// <summary>
/// A fluent builder for defining and creating a <see cref="Job"/>.
/// </summary>
public sealed class JobBuilder(
    ILogger logger,
    IJobRegistry jobRegistry,
    string name,
    IScriptContext context,
    Component component,
    ArtifactBuilder? artifactBuilder
) : IBuildable<Job> {
    private readonly ILogger _logger = logger;
    private readonly IJobRegistry _jobRegistry = jobRegistry;

    /// <summary>Gets the simple name of the job.</summary>
    public readonly string Name = name;

    /// <summary>Gets the artifact builder this job belongs to, if any.</summary>
    public readonly ArtifactBuilder? ArtifactBuilder = artifactBuilder;

    /// <summary>Gets the user-provided description of what the job does.</summary>
    public string JobDescription { get; private set; } = string.Empty;

    /// <summary>Gets a value indicating whether this job must run serially, blocking other jobs.</summary>
    public bool Blocking { get; private set; } = true;

    /// <summary>Gets the list of job identifiers that this job depends on.</summary>
    public readonly List<string> Dependencies = [];

    /// <summary>Gets the collection of actions to be executed by this job.</summary>
    public IJobAction[] Actions { get; private set; } = [];

    /// <summary>Gets the script context in which this job was defined.</summary>
    public readonly IScriptContext ScriptContext = context;

    /// <summary>Gets the component (e.g., Project or Workspace) this job belongs to.</summary>
    public readonly Component Component = component;

    /// <summary>
    /// Adds a dependency on another job. This job will not start until the specified job has completed.
    /// </summary>
    /// <param name="dependency">The unique identifier of the job to depend on.</param>
    /// <returns>The current builder instance for chaining.</returns>
    /// <exception cref="ConfigurationException">Thrown if a fully qualified dependency identifier is invalid.</exception>
    public JobBuilder After(string dependency) {
        // If the identifier is fully qualified (e.g., "project/artifact:job" or "plugin:component:job")
        if (dependency.Contains(':') || dependency.Contains('/')) {
            if (!RegexUtils.IsValidJob(dependency)) {
                throw new ConfigurationException($"Invalid job dependency format: '{dependency}'.");
            }
            Dependencies.Add(dependency);
        } else // Otherwise, resolve it relative to the current context.
          {
            var contextId = (Component is Workspace) ? null : Component.GetIdentifier();
            var match = new RegexUtils.JobMatch(contextId, ArtifactBuilder?.Name, dependency);
            Dependencies.Add(match.Format());
        }

        return this;
    }

    /// <summary>
    /// Adds multiple job dependencies.
    /// </summary>
    /// <param name="dependencies">The unique identifiers of the jobs to depend on.</param>
    /// <returns>The current builder instance for chaining.</returns>
    public JobBuilder After(params string[] dependencies) {
        foreach (var dep in dependencies) {
            After(dep); // Reuse the logic from the single-item overload.
        }
        return this;
    }

    /// <summary>
    /// Defines the action(s) to be executed by this job. This method can only be called once.
    /// </summary>
    /// <param name="action">A script function, a native <see cref="IJobAction"/>, or an array of these types.</param>
    /// <returns>The current builder instance for chaining.</returns>
    /// <exception cref="ConfigurationException">Thrown if the provided action or list contains an invalid type.</exception>
    public JobBuilder Execute(object action) {
        Actions = action switch {
            IList<object> list => [.. list.Select((item, index) => (item switch {
                IJobAction a => a,
                ScriptObject s => new JobScriptAction(s),
                _ => throw new ConfigurationException(
                    $"Invalid action type '{item?.GetType().Name ?? "null"}' at index {index} for job '{Name}'.")
            }))],
            ScriptObject scriptObj => [new JobScriptAction(scriptObj)],
            IJobAction nativeAction => [nativeAction],
            _ => throw new ConfigurationException(
                $"Unsupported action type '{action?.GetType().Name ?? "null"}' for job '{Name}'.")
        };
        return this;
    }

    public JobBuilder Description(string description) {
        JobDescription = description ?? string.Empty;
        return this;
    }

    /// <summary>
    /// Configures the job to run in the background (non-blocking). By default, jobs are blocking.
    /// </summary>
    /// <param name="isBackground">If <c>true</c>, the job will be non-blocking.</param>
    /// <returns>The current builder instance for chaining.</returns>
    public JobBuilder Background(bool isBackground = true) {
        Blocking = !isBackground;
        return this;
    }

    /// <summary>
    /// Builds the final <see cref="Job"/> instance and registers it with the job registry.
    /// </summary>
    /// <returns>The built and registered job.</returns>
    public Job Build() {
        var job = new Job(_logger, _jobRegistry, this);
        _jobRegistry.RegisterJob(job);
        return job;
    }
}
