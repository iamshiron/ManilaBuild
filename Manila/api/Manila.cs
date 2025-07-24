using System.Diagnostics.CodeAnalysis;
using System.Reflection;
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
using Shiron.Manila.Utils;

namespace Shiron.Manila.API;

/// <summary>
/// Primary API class exposing global Manila functions.
/// </summary>
public sealed class Manila(BaseServiceCotnainer baseServices, ServiceContainer services, ScriptContext context, WorkspaceScriptBridge workspaceBridge, Workspace workspace, ProjectScriptBridge? projectBridge, Project? project) {
    private readonly ServiceContainer _services = services;
    private readonly BaseServiceCotnainer _baseServices = baseServices;
    private readonly ScriptContext _context = context;

    private readonly Project? _project = project;
    private readonly Workspace _workspace = workspace;
    private readonly ProjectScriptBridge? _projectBridge = projectBridge;
    private readonly WorkspaceScriptBridge _workspaceBridge = workspaceBridge;

    /// <summary>
    /// The current build configuration for this Manila instance.
    /// </summary>
    public object? BuildConfig { get; private set; } = null;

    /// <summary>
    /// The list of job builders in this Manila instance.
    /// </summary>
    public List<JobBuilder> JobBuilders { get; } = [];

    /// <summary>
    /// The list of artifact builders in this Manila instance.
    /// </summary>
    public List<ArtifactBuilder> ArtifactBuilders { get; } = [];

    /// <summary>
    /// The active artifact builder context.
    /// </summary>
    public ArtifactBuilder? CurrentArtifactBuilder { get; set; } = null;

    /// <summary>
    /// Gets the current Manila project or throws if none exists.
    /// </summary>
    public ProjectScriptBridge GetProject() {
        return _projectBridge ?? throw new ContextException(Context.WORKSPACE, Context.PROJECT);
    }

    /// <summary>
    /// Creates an unresolved project reference by name.
    /// </summary>
    public UnresolvedProject GetProject(string name) {
        return new UnresolvedProject(_workspace, name);
    }

    /// <summary>
    /// Gets the current Manila workspace.
    /// </summary>
    public WorkspaceScriptBridge GetWorkspace() {
        return _workspaceBridge;
    }

    /// <summary>
    /// Gets the build configuration or throws if not set.
    /// </summary>
    public object GetConfig() {
        return BuildConfig ?? throw new ScriptingException("Cannot retreive build config before applying a language component!");
    }

    /// <summary>
    /// Creates a source set with the given origin.
    /// </summary>
    public SourceSetBuilder SourceSet(string origin) {
        return new(origin);
    }
    /// <summary>
    /// Creates an artifact using the provided configuration lambda.
    /// </summary>
    public ArtifactBuilder Artifact(ScriptObject obj) {
        if (_project == null) throw new ContextException(Context.WORKSPACE, Context.PROJECT);
        if (BuildConfig == null) throw new ManilaException("Cannot apply artifact when no language has been applied!");
        var builder = new ArtifactBuilder(_workspace, obj, this, (BuildConfig) BuildConfig, _project);
        ArtifactBuilders.Add(builder);
        return builder;
    }

    /// <summary>
    /// Pauses execution for the specified milliseconds.
    /// </summary>
    public async Task Sleep(int milliseconds) {
        await Task.Delay(milliseconds);
    }

    /// <summary>
    /// Creates a job with the given name.
    /// </summary>
    public JobBuilder Job(string name) {
        var applyTo = _project ?? (Component) _workspace;

        if (CurrentArtifactBuilder != null) {
            var jobBuilder = new JobBuilder(_baseServices.Logger, _services.JobRegistry, name, _context, applyTo, CurrentArtifactBuilder);
            CurrentArtifactBuilder.JobBuilders.Add(jobBuilder);
            return jobBuilder;
        }

        try {
            var builder = new JobBuilder(_baseServices.Logger, _services.JobRegistry, name, _context, applyTo, null);
            JobBuilders.Add(builder);
            return builder;
        } catch (ContextException e) {
            if (e.Is != Context.WORKSPACE) throw;
            var builder = new JobBuilder(_baseServices.Logger, _services.JobRegistry, name, _context, _workspace, null);
            JobBuilders.Add(builder);
            return builder;
        }
    }

    /// <summary>
    /// Creates a directory handle for the given path.
    /// </summary>
    public DirHandle Dir(string path) {
        return new DirHandle(path);
    }

    /// <summary>
    /// Creates a file handle for the given path.
    /// </summary>
    public FileHandle File(string path) {
        return new FileHandle(path);
    }

    /// <summary>
    /// Registers an action to run on projects matching the given filter.
    /// </summary>
    public void OnProject(object o, dynamic a) {
        using (new ProfileScope(_baseServices.Profiler, MethodBase.GetCurrentMethod()!)) {
            var filter = ProjectFilter.From(_baseServices.Logger, o);
            _workspace.ProjectFilters.Add(new Tuple<ProjectFilter, Action<Project>>(filter, (project) => a(project)));
        }
    }

    /// <summary>
    /// Executes the job identified by the given key.
    /// </summary>
    public async Task RunJob(string key) {
        var job = _services.JobRegistry.GetJob(key) ?? throw new Exception("Job not found: " + key);
        await job.ExecuteAsync();
    }

    /// <summary>
    /// Builds the project using its language component.
    /// </summary>
    public async Task Build(WorkspaceScriptBridge workspaceBridge, ProjectScriptBridge projectBridge, BuildConfig config, UnresolvedArtifactScriptBridge unresolvedArtifact) {
        var workspace = workspaceBridge._handle;
        var project = projectBridge._handle;
        var artifact = unresolvedArtifact.Resolve();

        artifact = await _services.ArtifactManager.AppendCahedDataAsync(artifact, config, project);

        using (new ProfileScope(_baseServices.Profiler, MethodBase.GetCurrentMethod()!)) {
            var logCache = new LogCache();

            using var logInjector = new LogInjector(
                _baseServices.Logger,
                logCache.Entries.Add
            );

            var res = project.GetLanguageComponent().Build(workspace, project, config, artifact, _services.ArtifactManager);

            if (res is BuildExitCodeSuccess) {
                _baseServices.Logger.Info($"Build successful for {project.Name} with artifact {artifact.Name}");
                artifact.LogCache = logCache;

                await _services.ArtifactManager.CacheArtifactAsync(artifact, config, project);
            } else if (res is BuildExitCodeCached cached) {
                _baseServices.Logger.Info($"Loaded cached build for {project.Name} with artifact {artifact.Name}.");

                if (artifact.LogCache is null) {
                    _baseServices.Logger.Error($"Artifact '{artifact.Name}' has no log cache, this is unexpected!");
                    return;
                }

                _baseServices.Logger.Debug($"Current context ID: {_baseServices.Logger.LogContext.CurrentContextID}");
                artifact.LogCache.Replay(_baseServices.Logger, _baseServices.Logger.LogContext.CurrentContextID ?? Guid.Empty);
            } else if (res is BuildExitCodeFailed failed) {
                _baseServices.Logger.Error($"Build failed for {project.Name} with artifact {artifact.Name}: {failed.Exception.Message}");
            }
        }
    }

    /// <summary>
    /// Resolves and runs the specified unresolved project.
    /// </summary>
    public void Run(UnresolvedProject project) {
        Run(project.Resolve());
    }

    /// <summary>
    /// Runs the specified project using its language component.
    /// </summary>
    public void Run(Project project) {
        using (new ProfileScope(_baseServices.Profiler, MethodBase.GetCurrentMethod()!)) {
            project.GetLanguageComponent().Run(project);
        }
    }

    /// <summary>
    /// Retrieves the value of the specified environment variable.
    /// </summary>
    public string GetEnv(string key, string? defaultValue = null) {
        return _context.GetEnvironmentVariable(key) ?? defaultValue ?? throw new Exception($"Environment variable {key} is not set.");
    }

    /// <summary>
    /// Retrieves the specified environment variable as a double or zero if unset.
    /// </summary>
    public double GetEnvNumber(string key, double? defaultValue = null) {
        var value = _context.GetEnvironmentVariable(key);
        return string.IsNullOrEmpty(value)
            ? 0
            : double.TryParse(value, out var result) ? result : defaultValue ?? throw new Exception($"Environment variable {key} is not a number: {value}");
    }
    /// <summary>
    /// Retrieves the specified environment variable as a boolean or false if unset.
    /// </summary>

    public bool GetEnvBool(string key, bool? defaultValue = null) {
        var value = _context.GetEnvironmentVariable(key);
        return string.IsNullOrEmpty(value)
            ? false
            : bool.TryParse(value, out var result) ? result : defaultValue ?? throw new Exception($"Environment variable {key} is not a number: {value}");
    }
    /// <summary>
    /// Sets the specified environment variable to the given value.
    /// </summary>

    public void SetEnv(string key, string value) {
        _context.SetEnvironmentVariable(key, value);
    }

    /// <summary>
    /// Imports an API type instance for the given key.
    /// </summary>
    public object Import(string key) {
        using (new ProfileScope(_baseServices.Profiler, MethodBase.GetCurrentMethod()!)) {
            var t = Activator.CreateInstance(_services.ExtensionManager.GetAPIType(key));
            _baseServices.Logger.Debug($"Importing {key} as {t}");

            if (t == null)
                throw new Exception($"Failed to import API type for key: {key}");

            return t;
        }
    }

    /// <summary>
    /// Applies a language component by its URI.
    /// </summary>
    public void Apply(string uri) {
        if (_project == null) throw new ContextException(Context.WORKSPACE, Context.PROJECT);

        var match = RegexUtils.MatchPluginComponent(uri) ?? throw new ScriptingException($"Invalid component URI: {uri}");
        var component = _services.ExtensionManager.GetPluginComponent(match.Group ?? "shiron.manila", match.Plugin, match.Component, match.Version);
        if (component is not LanguageComponent) throw new ScriptingException($"Component {uri} is not a language component.");

        var langComp = (LanguageComponent) component;
        _baseServices.Logger.Debug($"Applying language component {langComp.Name} from {langComp._plugin}");
        _project.PluginComponents.Add(langComp.GetType(), langComp);

        var buildConfig = Activator.CreateInstance(langComp.BuildConfigType) ?? throw new ScriptingException($"Failed to create build config for language component {langComp.Name}.");
        BuildConfig = buildConfig;
    }

    #region Job Actions

    /// <summary>
    /// Creates a shell-based job action with cmd.exe.
    /// </summary>
    public static IJobAction Shell(string command) {
        return new JobShellAction(new(
            "cmd.exe",
            ["/c", .. command.Split(" ")]
        ));
    }
    /// <summary>
    /// Creates a job action to execute the given command.
    /// </summary>
    public static IJobAction Execute(string command) {
        return new JobShellAction(new(
            command.Split(" ")[0],
            command.Split(" ")[1..]
        ));
    }

    #endregion
}
