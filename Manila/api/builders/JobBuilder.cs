using System.Diagnostics.CodeAnalysis;
using Microsoft.ClearScript;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Logging;
using Shiron.Manila.Registries;
using Shiron.Manila.Utils;

namespace Shiron.Manila.API.Builders;

/// <summary>
/// Builder for creating jobs within a Manila build configuration.
/// </summary>
public sealed class JobBuilder(ILogger logger, IJobRegistry jobRegistry, string name, ScriptContext context, Component component, ArtifactBuilder? artifactBuilder) : IBuildable<Job> {
    private readonly ILogger _logger = logger;
    private readonly IJobRegistry _jobRegistry = jobRegistry;

    /// <summary>
    /// The name of the job.
    /// </summary>
    public readonly string Name = name;

    /// <summary>
    /// The artifact builder this job belongs to, if any.
    /// </summary>
    public readonly ArtifactBuilder? ArtifactBuilder = artifactBuilder;

    /// <summary>
    /// Description of what the job does.
    /// </summary>
    public string Description { get; private set; } = "A generic job";

    /// <summary>
    /// Whether the job blocks execution flow until completion.
    /// </summary>
    public bool Blocking { get; private set; } = true;

    /// <summary>
    /// List of job dependencies that must execute before this job.
    /// </summary>
    public readonly List<string> Dependencies = [];

    /// <summary>
    /// Array of actions to be executed by this job.
    /// </summary>
    public IJobAction[] Actions { get; private set; } = [];

    /// <summary>
    /// The script context for this job.
    /// </summary>
    public readonly ScriptContext ScriptContext = context;

    /// <summary>
    /// The component this job belongs to.
    /// </summary>
    public readonly Component Component = component;

    /// <summary>
    /// Add a dependency to the job.
    /// </summary>
    /// <param name="job">The dependents job ID</param>
    /// <returns>Job instance for chaining calls</returns>
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Exposed to JavaScript context")]
    public JobBuilder after(string job) {
        if (job.Contains(":") || job.Contains("/")) {
            if (!RegexUtils.IsValidJob(job)) throw new ManilaException($"Invalid job regex {job}!");
            Dependencies.Add(job);
            return this;
        }

        var match = new RegexUtils.JobMatch(Component is Workspace ? null : Component.GetIdentifier(), ArtifactBuilder?.Name, job);
        Dependencies.Add(match.Format());

        return this;
    }
    /// <summary>
    /// Add a dependency to the job.
    /// </summary>
    /// <param name="job">The dependents job ID</param>
    /// <returns>Job instance for chaining calls</returns>
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Exposed to JavaScript context")]
    public JobBuilder after(string[] job) {
        foreach (var t in job) after(t);
        return this;
    }
    /// <summary>
    /// The action to be executed by the job.
    /// </summary>
    /// <param name="action">The action</param>
    /// <returns>Job instance for chaining calls</returns>
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Exposed to JavaScript context")]
    public JobBuilder execute(object o) {
        _logger.System($"Adding action to job {Name} in ({o.GetType().FullName})");
        if (o is IJobAction action) {
            _logger.Debug($"Found job action of type {action.GetType().FullName}");
            Actions = [action];
        } else if (o is IList<object> list) {
            _logger.Debug($"Found {list.Count} chained actions!");
            Actions = list.Cast<IJobAction>().ToArray();
        } else {
            var obj = (ScriptObject) o;
            Actions = [new JobScriptAction(obj)];
        }

        return this;
    }
    /// <summary>
    /// Set the description of the job.
    /// </summary>
    /// <param name="description">The description</param>
    /// <returns>Job instance for chaining calls</returns>
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Exposed to JavaScript context")]
    public JobBuilder description(string description) {
        this.Description = description;
        return this;
    }
    /// <summary>
    /// Sets a job's blocking mode, meaning if it will block the execution flow or is running in the background
    /// </summary>
    /// <param name="background">True: Non Blocking, False: Blocking</param>
    /// <returns></returns>
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Exposed to JavaScript context")]
    public JobBuilder background(bool background = true) {
        this.Blocking = !background;
        return this;
    }

    /// <summary>
    /// Builds the job using the configured properties and actions.
    /// </summary>
    /// <returns>The built job instance.</returns>
    public Job Build() {
        var job = new Job(_logger, _jobRegistry, this);
        _jobRegistry.RegisterJob(job);
        return job;
    }
}
