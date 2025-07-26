using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ClearScript;
using Shiron.Manila.API.Bridges;
using Shiron.Manila.API.Builders;
using Shiron.Manila.Artifacts;
using Shiron.Manila.Caching;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Ext;
using Shiron.Manila.Logging;
using Shiron.Manila.Profiling;
using Shiron.Manila.Registries;

namespace Shiron.Manila.API;

/// <summary>
/// Defines the primary, script-facing API for interacting with the Manila build system.
/// </summary>
public sealed class Manila(
    BaseServiceContainer baseServices,
    ServiceContainer services,
    ScriptContext context,
    WorkspaceScriptBridge workspaceBridge,
    Workspace workspace,
    ProjectScriptBridge? projectBridge,
    Project? project) {
    private readonly ServiceContainer _services = services;
    private readonly BaseServiceContainer _baseServices = baseServices;
    private readonly ScriptContext _context = context;
    private readonly Project? _project = project;
    private readonly Workspace _workspace = workspace;
    private readonly ProjectScriptBridge? _projectBridge = projectBridge;
    private readonly WorkspaceScriptBridge _workspaceBridge = workspaceBridge;

    // A private record for storing project filters and their associated actions.
    private record ProjectHook(ProjectFilter Filter, Action<Project> Action);

    /// <summary>Gets or sets the current language-specific build configuration.</summary>
    public object? BuildConfig { get; private set; }

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
    public object GetConfig() => BuildConfig
        ?? throw new InvalidOperationException("A language component must be applied before accessing the build configuration.");

    /// <summary>
    /// Applies a language component to the current project, enabling language-specific tasks.
    /// </summary>
    /// <param name="uri">The URI of the language component (e.g., "manila-dotnet:language").</param>
    /// <exception cref="InvalidOperationException">Thrown if not in a project context.</exception>
    /// <exception cref="PluginException">Thrown if the component cannot be found or is not a language component.</exception>
    public void Apply(string uri) {
        if (_project is null)
            throw new InvalidOperationException("`Apply` can only be used within a project context.");

        var component = _services.ExtensionManager.GetPluginComponent(uri);
        if (component is not LanguageComponent langComp)
            throw new PluginException($"The component '{uri}' is not a valid language component.");

        _project.PluginComponents.Add(langComp.GetType(), langComp);

        try {
            BuildConfig = Activator.CreateInstance(langComp.BuildConfigType)
                ?? throw new PluginException($"Failed to create build configuration of type '{langComp.BuildConfigType.Name}'.");
        } catch (Exception e) {
            throw new PluginException($"Failed to instantiate build configuration for '{langComp.Name}'.", e);
        }
    }

    /// <summary>Imports a C# type registered by a plugin, making it available to the script.</summary>
    /// <param name="key">The key the API type was registered with.</param>
    /// <exception cref="PluginException">Thrown if the key is not found or the type cannot be instantiated.</exception>
    public object Import(string key) {
        var type = _services.ExtensionManager.GetAPIType(key);
        try {
            return Activator.CreateInstance(type)
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
    public ArtifactBuilder Artifact(ScriptObject configurator) {
        if (_project is null)
            throw new InvalidOperationException("Artifacts can only be defined within a project context.");
        if (BuildConfig is null)
            throw new InvalidOperationException("A language must be applied with `apply()` before defining an artifact.");

        var builder = new ArtifactBuilder(_workspace, configurator, this, (BuildConfig) BuildConfig, _project);
        ArtifactBuilders.Add(builder);
        return builder;
    }

    /// <summary>Begins the definition of a job.</summary>
    /// <param name="name">The name of the job, unique within its scope (artifact or project).</param>
    public JobBuilder Job(string name) {
        var component = _project ?? (Component) _workspace;

        // If we are inside an artifact's configuration block, associate this job with it.
        if (CurrentArtifactBuilder is not null) {
            var artifactJobBuilder = new JobBuilder(_baseServices.Logger, _services.JobRegistry, name, _context, component, CurrentArtifactBuilder);
            CurrentArtifactBuilder.JobBuilders.Add(artifactJobBuilder);
            return artifactJobBuilder;
        }

        // Otherwise, it's a project- or workspace-level job.
        var jobBuilder = new JobBuilder(_baseServices.Logger, _services.JobRegistry, name, _context, component, null);
        JobBuilders.Add(jobBuilder);
        return jobBuilder;
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

        return new JobShellAction(_baseServices.Logger, new(shell, [shellArg, .. command.Split(' ')]));
    }

    /// <summary>Creates a job action that executes a program directly.</summary>
    /// <param name="executable">The program or script to execute.</param>
    /// <param name="args">The arguments to pass to the program.</param>
    public IJobAction Execute(string command) =>
        new JobShellAction(_baseServices.Logger, new(
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

    /// <summary>Runs a specific, fully-resolved project's default `run` task.</summary>
    public void Run(Project project) => project.GetLanguageComponent().Run(project);

    /// <summary>Resolves and runs a project's default `run` task.</summary>
    public void Run(UnresolvedProject project) => Run(project.Resolve());

    /// <summary>Executes a single job by its fully qualified identifier.</summary>
    /// <param name="key">The unique identifier of the job to run.</param>
    /// <exception cref="ConfigurationException">Thrown if the job is not found.</exception>
    public async Task RunJob(string key) {
        var job = _services.JobRegistry.GetJob(key)
            ?? throw new ConfigurationException($"The job '{key}' was not found in the registry.");
        await job.RunAsync();
    }

    /// <summary>Executes the build process for a given artifact.</summary>
    /// <exception cref="BuildProcessException">Thrown if the build fails.</exception>
    public async Task Build(WorkspaceScriptBridge workspaceBridge, ProjectScriptBridge projectBridge, BuildConfig config, UnresolvedArtifactScriptBridge unresolvedArtifact) {
        var workspace = workspaceBridge._handle;
        var project = projectBridge._handle;
        var artifact = await _services.ArtifactManager.AppendCachedDataAsync(
            unresolvedArtifact.Resolve(), config, project
        );

        var logCache = new LogCache();
        using (new LogInjector(_baseServices.Logger, logCache.Entries.Add)) {
            var result = project.GetLanguageComponent().Build(
                workspace, artifact.Project, config, artifact, _services.ArtifactManager
            );

            switch (result) {
                case BuildExitCodeSuccess:
                    artifact.LogCache = logCache;
                    await _services.ArtifactManager.CacheArtifactAsync(artifact, config, artifact.Project);
                    break;

                case BuildExitCodeCached:
                    if (artifact.LogCache is { } cache) {
                        cache.Replay(_baseServices.Logger, _baseServices.Logger.LogContext.CurrentContextID ?? Guid.Empty);
                    } else {
                        _baseServices.Logger.Warning($"Cached artifact '{artifact.Name}' was missing its log cache.");
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
    public Task Sleep(int milliseconds) => Task.Delay(milliseconds);

    /// <summary>Creates a handle to a directory path.</summary>
    public DirHandle Dir(string path) => new(path);

    /// <summary>Creates a handle to a file path.</summary>
    public FileHandle File(string path) => new(path);

    #endregion
}
