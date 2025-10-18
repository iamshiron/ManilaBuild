using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ClearScript;
using Shiron.Manila.API.Bridges;
using Shiron.Manila.API.Builders;
using Shiron.Manila.API.Dependencies;
using Shiron.Manila.API.Ext;
using Shiron.Manila.API.Interfaces;
using Shiron.Manila.API.Interfaces.Artifacts;
using Shiron.Manila.API.Logging;
using Shiron.Manila.API.Utils;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Interfaces;
using Shiron.Manila.Logging;
using Shiron.Manila.Profiling;

namespace Shiron.Manila.API;

public sealed class ManilaAPIFlags {
    public bool InvalidateBuildCache { get; set; } = false;
}

/// <summary>
/// Defines the primary, script-facing API for interacting with the Manila build system.
/// </summary>
public sealed class Manila(
        ManilaAPIFlags flags,
        APIServiceContainer services,
        IScriptContext context,
        WorkspaceScriptBridge workspaceBridge,
        Workspace workspace,
        ProjectScriptBridge? projectBridge,
        Project? project
) {
    public readonly ManilaAPIFlags Flags = flags;
    private readonly APIServiceContainer _services = services;
    private readonly IScriptContext _context = context;
    private readonly Project? _project = project;
    private readonly Workspace _workspace = workspace;
    private readonly ProjectScriptBridge? _projectBridge = projectBridge;
    private readonly WorkspaceScriptBridge _workspaceBridge = workspaceBridge;

    // A private record for storing project filters and their associated actions.
    private record ProjectHook(ProjectFilter Filter, Action<Project> Action);

    /// <summary>Gets the list of job builders defined at the project or workspace level.</summary>
    public List<JobBuilder> JobBuilders { get; } = [];

    /// <summary>Gets the list of artifact builders defined in the current context.</summary>
    public List<ArtifactBuilder> ArtifactBuilders { get; } = [];

    /// <summary>
    /// Gets or sets the active artifact builder. This is used implicitly by `Job()` calls
    /// to associate jobs with the correct artifact.
    /// </summary>
    internal ArtifactBuilder? CurrentArtifactBuilder { get; set; }

    #region Context & Configuration

    /// <summary>Gets a bridge to the current project context.</summary>
    /// <exception cref="InvalidOperationException">Thrown if not currently in a project context.</exception>
    public ProjectScriptBridge GetProject() => _projectBridge
        ?? throw new InvalidOperationException("This operation is only valid within a project context (e.g., a project's Manila.js file).");

    /// <summary>Creates a lazy-loading reference to another project in the workspace.</summary>
    /// <param name="name">The name (identifier) of the project to reference.</param>
    public UnresolvedProject GetProject(string name) => new(_workspace, name);

    /// <summary>Gets a bridge to the current workspace context.</summary>
    public WorkspaceScriptBridge GetWorkspace() => _workspaceBridge;

    /// <summary>Gets the active build configuration provided by a language plugin.</summary>
    /// <exception cref="InvalidOperationException">Thrown if a language plugin has not yet been applied.</exception>
    public object GetConfig(UnresolvedArtifactScriptBridge artifact) {
        var componentMatch = artifact.PluginComponent
            ?? throw new InvalidOperationException("Artifact must specify a plugin component to get its build configuration.");
        var builder = _services.ExtensionManager.GetArtifact(componentMatch)
            ?? throw new ConfigurationException($"Plugin component '{componentMatch}' not found for artifact '{artifact.ArtifactID}'.");

        var config = Activator.CreateInstance(builder.BuildConfigType) ??
            throw new PluginException($"Activator failed to create an instance of '{builder.BuildConfigType.Name}' for artifact '{artifact.ArtifactID}'.");

        return config;
    }

    /// <summary>Imports a C# type registered by a plugin, making it available to the script.</summary>
    /// <param name="key">The key the API type was registered with.</param>
    /// <exception cref="PluginException">Thrown if the key is not found or the type cannot be instantiated.</exception>
    public dynamic Import(string key) {
        var type = _services.ExtensionManager.GetAPIType(key);
        try {
            var instance = Activator.CreateInstance(type);
            return instance
                ?? throw new PluginException($"Activator failed to create an instance of '{type.Name}' for key '{key}'.");
        } catch (Exception e) {
            throw new PluginException($"Failed to import API for key '{key}'. See inner exception for details.", e);
        }
    }

    #endregion

    #region Builders

    /// <summary>Begins the definition of an artifact.</summary>
    /// <param name="name">The name of the artifact.</param>
    /// <param name="configurator">A script function that configures the artifact.</param>
    /// <exception cref="InvalidOperationException">Thrown if not in a project context or if a language has not been applied.</exception>
    public ArtifactBuilder Artifact(string baseComponent, ScriptObject configurator) {
        if (_project is null)
            throw new InvalidOperationException("Artifacts can only be defined within a project context.");

        var builder = new ArtifactBuilder(_workspace, baseComponent, configurator, this, _project);
        ArtifactBuilders.Add(builder);
        return builder;
    }

    /// <summary>Begins the definition of a job.</summary>
    /// <param name="name">The name of the job, unique within its scope (artifact or project).</param>
    public JobBuilder Job(string name) {
        var component = _project ?? (Component) _workspace;

        // If we are inside an artifact's configuration block, associate this job with it.
        if (CurrentArtifactBuilder is not null) {
            var artifactJobBuilder = new JobBuilder(_services.Logger, _services.JobRegistry, name, _context, component, CurrentArtifactBuilder);
            CurrentArtifactBuilder.JobBuilders.Add(artifactJobBuilder);
            return artifactJobBuilder;
        }

        // Otherwise, it's a project- or workspace-level job.
        var jobBuilder = new JobBuilder(_services.Logger, _services.JobRegistry, name, _context, component, null);
        JobBuilders.Add(jobBuilder);
        return jobBuilder;
    }

    public void Log(string message) {
        _services.Logger.Log(new ScriptLogEntry(_context.ScriptPath, message, _context.ContextID));
    }

    /// <summary>Creates a file set builder for defining collections of files.</summary>
    /// <param name="origin">The root directory for the source set, relative to the project root.</param>
    public SourceSetBuilder SourceSet(string origin) => new(origin);

    #endregion

    #region Job Actions

    /// <summary>Creates a job action that executes a command via the default system shell (e.g., cmd.exe, /bin/sh).</summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="args">The arguments to pass to the command.</param>
    public IJobAction Shell(string command) {
        var shell = Environment.OSVersion.Platform == PlatformID.Win32NT ? "cmd.exe" : "/bin/sh";
        var shellArg = Environment.OSVersion.Platform == PlatformID.Win32NT ? "/c" : "-c";

        return new JobShellAction(_services.Logger, new(shell, [shellArg, .. command.Split(' ')]));
    }

    /// <summary>Creates a job action that executes a program directly.</summary>
    /// <param name="executable">The program or script to execute.</param>
    /// <param name="args">The arguments to pass to the program.</param>
    public IJobAction Execute(string command) =>
        new JobShellAction(_services.Logger, new(
            command.Split(" ")[0],
            command.Split(" ")[1..]
        ));

    #endregion

    #region Execution & Hooks

    /// <summary>Registers an action to run on all projects that match the given filter.</summary>
    /// <param name="filterObject">A filter (e.g., "*", "my-project", a RegExp) to select projects.</param>
    /// <param name="action">A script function that receives a project bridge object.</param>
    public void OnProject(object filterObject, dynamic action) {
        var filter = ProjectFilter.From(filterObject);
        _workspace.ProjectFilters.Add(new ProjectFilterHook(filter, project => action(new ProjectScriptBridge(project))));
    }

    /// <summary>Executes a single job by its fully qualified identifier.</summary>
    /// <param name="key">The unique identifier of the job to run.</param>
    /// <exception cref="ConfigurationException">Thrown if the job is not found.</exception>
    public async Task RunJob(string key) {
        var job = _services.JobRegistry.GetJob(key)
            ?? throw new ConfigurationException($"The job '{key}' was not found in the registry.");
        await job.RunAsync();
    }

    /// <summary>Builds a specific, fully-resolved project's artifact.</summary>
    public async Task Build(ProjectScriptBridge projectBridge, BuildConfig config, UnresolvedArtifactScriptBridge artifactBridge) {
        var project = projectBridge._handle;
        var artifact = await _services.ArtifactManager.AppendCachedDataAsync(
            artifactBridge.Resolve(), config, project
        );

        var artifactBlueprint = _services.ExtensionManager.GetArtifact(artifact.PluginComponent)
            ?? throw new ConfigurationException($"Artifact builder '{artifact.PluginComponent}' not found for artifact '{artifact.Name}'.");

        var logCache = new LogCache();
        var currentCtx = _services.Logger.LogContext.CurrentContextID;
        using (currentCtx is Guid ctx
            ? _services.Logger.CreateContextInjector(logCache.Entries.Add, ctx)
            : new LogInjector(_services.Logger, logCache.Entries.Add)) {
            var res = _services.ArtifactManager.BuildFromDependencies(
                artifactBlueprint,
                artifact,
                project,
                config,
                Flags.InvalidateBuildCache
            );

            switch (res) {
                case BuildExitCodeSuccess success:
                    artifact.LogCache = logCache;
                    await _services.ArtifactManager.CacheArtifactAsync(artifact, config, artifact.Project, success.Outputs);
                    break;

                case BuildExitCodeCached c:
                    if (artifact.LogCache is { } cache) {
                        _services.Logger.Debug($"Replaying log cache for artifact '{artifact.Name}' in project '{artifact.Project.Identifier}'.");
                        _services.ArtifactManager.UpdateCacheAccessTime(c);
                        cache.Replay(_services.Logger, _services.Logger.LogContext.CurrentContextID ?? Guid.Empty);
                    } else {
                        _services.Logger.Warning($"Cached artifact '{artifact.Name}' was missing its log cache.");
                    }
                    break;

                case BuildExitCodeFailed failed:
                    throw new BuildProcessException(
                        $"Build failed for artifact '{artifact.Name}' in project '{artifact.Project.Identifier}'.",
                        failed.Exception
                    );
            }
        }
    }

    /// <summary>Runs a specific, fully-resolved project's default `run` task.</summary>
    public void Run(ProjectScriptBridge project, UnresolvedArtifactScriptBridge artifact)
        => throw new NotImplementedException("This method is not yet implemented.");

    #endregion

    #region Environment & Utilities

    /// <summary>Gets an environment variable as a string.</summary>
    /// <param name="key">The name of the environment variable.</param>
    /// <param name="defaultValue">An optional value to return if the variable is not set.</param>
    /// <exception cref="ConfigurationException">Thrown if the variable is not set and no default value is provided.</exception>
    public string GetEnv(string key, string? defaultValue = null) => _context.GetEnvironmentVariable(key)
        ?? defaultValue
        ?? throw new ConfigurationException($"Required environment variable '{key}' is not set.");

    /// <summary>Gets an environment variable as a number.</summary>
    /// <param name="key">The name of the environment variable.</param>
    /// <param name="defaultValue">An optional value to return if the variable is not set or invalid.</param>
    /// <exception cref="ConfigurationException">Thrown if the variable is not a valid number and no default is provided.</exception>
    public double GetEnvNumber(string key, double? defaultValue = null) {
        var value = _context.GetEnvironmentVariable(key);
        if (string.IsNullOrEmpty(value)) return defaultValue ?? 0.0;

        return double.TryParse(value, out var result) ? result : defaultValue
            ?? throw new ConfigurationException($"Environment variable '{key}' is not a valid number: '{value}'.");
    }

    /// <summary>Gets an environment variable as a boolean.</summary>
    /// <param name="key">The name of the environment variable.</param>
    /// <param name="defaultValue">An optional value to return if the variable is not set or invalid.</param>
    /// <exception cref="ConfigurationException">Thrown if the variable is not a valid boolean and no default is provided.</exception>
    public bool GetEnvBool(string key, bool? defaultValue = null) {
        var value = _context.GetEnvironmentVariable(key);
        if (string.IsNullOrEmpty(value)) return defaultValue ?? false;

        return bool.TryParse(value, out var result) ? result : defaultValue
            ?? throw new ConfigurationException($"Environment variable '{key}' is not a valid boolean: '{value}'.");
    }

    /// <summary>Sets an environment variable for the current execution context.</summary>
    public void SetEnv(string key, string value) => _context.SetEnvironmentVariable(key, value);

    /// <summary>Pauses execution for the specified duration.</summary>
    public async Task Sleep(int milliseconds) {
        await Task.Delay(milliseconds);
    }

    /// <summary>Creates a handle to a directory path.</summary>
    public DirHandle Dir(string path) => new(path);

    /// <summary>Creates a handle to a file path.</summary>
    public FileHandle File(string path) => new(path);

    #endregion

    #region Dependencies

    public IDependency Artifact(UnresolvedProject project, string artifact) {
        return new ArtifactDependency(project, artifact);
    }

    #endregion
}
