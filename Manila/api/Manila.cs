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
public sealed class Manila(ServiceContainer services, ScriptContext context, WorkspaceScriptBridge workspaceBridge, Workspace workspace, ProjectScriptBridge? projectBridge, Project? project) : ExposedDynamicObject {
    private readonly ServiceContainer _services = services;
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
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Exposed to JavaScript context")]
    public ProjectScriptBridge getProject() {
        return _projectBridge ?? throw new ContextException(Context.WORKSPACE, Context.PROJECT);
    }

    /// <summary>
    /// Creates an unresolved project reference by name.
    /// </summary>
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Exposed to JavaScript context")]
    public UnresolvedProject getProject(string name) {
        return new UnresolvedProject(_workspace, name);
    }

    /// <summary>
    /// Gets the current Manila workspace.
    /// </summary>
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Exposed to JavaScript context")]
    public WorkspaceScriptBridge getWorkspace() {
        return _workspaceBridge;
    }

    /// <summary>
    /// Gets the build configuration or throws if not set.
    /// </summary>
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Exposed to JavaScript context")]
    public object getConfig() {
        return BuildConfig ?? throw new ScriptingException("Cannot retreive build config before applying a language component!");
    }

    /// <summary>
    /// Creates a source set with the given origin.
    /// </summary>
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Exposed to JavaScript context")]
    public SourceSetBuilder sourceSet(string origin) {
        return new(origin);
    }
    /// <summary>
    /// Creates an artifact using the provided configuration lambda.
    /// </summary>
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Exposed to JavaScript context")]
    public ArtifactBuilder artifact(dynamic lambda) {
        if (_project == null) throw new ContextException(Context.WORKSPACE, Context.PROJECT);
        if (BuildConfig == null) throw new ManilaException("Cannot apply artifact when no language has been applied!");
        var builder = new ArtifactBuilder(_workspace, () => lambda(), this, (BuildConfig) BuildConfig, _project.Name);
        ArtifactBuilders.Add(builder);
        return builder;
    }

    /// <summary>
    /// Pauses execution for the specified milliseconds.
    /// </summary>
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Exposed to JavaScript context")]
    public async Task sleep(int milliseconds) {
        await Task.Delay(milliseconds);
    }

    /// <summary>
    /// Creates a job with the given name.
    /// </summary>
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Exposed to JavaScript context")]
    public JobBuilder job(string name) {
        var applyTo = _project ?? (Component) _workspace;

        if (CurrentArtifactBuilder != null) {
            var jobBuilder = new JobBuilder(_services.Logger, _services.JobRegistry, name, _context, applyTo, CurrentArtifactBuilder);
            CurrentArtifactBuilder.JobBuilders.Add(jobBuilder);
            return jobBuilder;
        }

        try {
            var builder = new JobBuilder(_services.Logger, _services.JobRegistry, name, _context, applyTo, null);
            JobBuilders.Add(builder);
            return builder;
        } catch (ContextException e) {
            if (e.Is != Context.WORKSPACE) throw;
            var builder = new JobBuilder(_services.Logger, _services.JobRegistry, name, _context, _workspace, null);
            JobBuilders.Add(builder);
            return builder;
        }
    }

    /// <summary>
    /// Creates a directory handle for the given path.
    /// </summary>
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Exposed to JavaScript context")]
    public DirHandle dir(string path) {
        return new DirHandle(path);
    }

    /// <summary>
    /// Creates a file handle for the given path.
    /// </summary>
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Exposed to JavaScript context")]
    public FileHandle file(string path) {
        return new FileHandle(path);
    }

    /// <summary>
    /// Applies the plugin component identified by the given key.
    /// </summary>
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Exposed to JavaScript context")]
    public void apply(string pluginComponentKey) {
        var component = _services.ExtensionManager.GetPluginComponent(pluginComponentKey);
        apply(component);
    }

    /// <summary>
    /// Applies the plugin component defined in the script object.
    /// </summary>
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Exposed to JavaScript context")]
    public void apply(ScriptObject obj) {
        var version = obj.GetProperty("version");
        var component = _services.ExtensionManager.GetPluginComponent((string) obj["group"], (string) obj["name"], (string) obj["component"], version == Undefined.Value ? null : (string) version);
        apply(component);
    }

    /// <summary>
    /// Applies the provided plugin component to the current project.
    /// </summary>
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Exposed to JavaScript context")]
    public void apply(PluginComponent component) {
        if (_project == null || _projectBridge == null) throw new ContextException(Context.WORKSPACE, Context.PROJECT);

        using (new ProfileScope(_services.Profiler, MethodBase.GetCurrentMethod()!)) {
            _services.Logger.Debug("Applying: " + component);
            ScriptBridgeContextApplyer.ApplyComponent(_services.Logger, _context, _projectBridge, _project, _workspace, component);
            if (component is LanguageComponent lc) {
                BuildConfig = Activator.CreateInstance(lc.BuildConfigType) ?? throw new ManilaException("Unable to assign build config");
            }
        }
    }

    /// <summary>
    /// Registers an action to run on projects matching the given filter.
    /// </summary>
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Exposed to JavaScript context")]
    public void onProject(object o, dynamic a) {
        using (new ProfileScope(_services.Profiler, MethodBase.GetCurrentMethod()!)) {
            var filter = ProjectFilter.From(_services.Logger, o);
            _workspace.ProjectFilters.Add(new Tuple<ProjectFilter, Action<Project>>(filter, (project) => a(project)));
        }
    }

    /// <summary>
    /// Executes the job identified by the given key.
    /// </summary>
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Exposed to JavaScript context")]
    public async Task runJob(string key) {
        var job = _services.JobRegistry.GetJob(key) ?? throw new Exception("Job not found: " + key);
        await job.Execute();
    }

    /// <summary>
    /// Builds the project using its language component.
    /// </summary>
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Exposed to JavaScript context")]
    public void build(WorkspaceScriptBridge workspaceBridge, ProjectScriptBridge projectBridge, BuildConfig config, string artifactID) {
        var workspace = workspaceBridge._handle;
        var project = projectBridge._handle;
        var artifact = project.Artifacts[artifactID];
        artifact = _services.ArtifactManager.AppendCahedData(artifact, config, project);

        using (new ProfileScope(_services.Profiler, MethodBase.GetCurrentMethod()!)) {
            var logCache = new LogCache();

            using var logInjector = new LogInjector(
                _services.Logger,
                logCache.Entries.Add
            );

            var res = project.GetLanguageComponent().Build(workspace, project, config, artifact, _services.ArtifactManager);

            if (res is BuildExitCodeSuccess) {
                _services.Logger.Info($"Build successful for {project.Name} with artifact {artifactID}");
                artifact.LogCache = logCache;

                _services.ArtifactManager.CacheArtifact(artifact, config, project);
            } else if (res is BuildExitCodeCached cached) {
                _services.Logger.Info($"Loaded cached build for {project.Name} with artifact {artifactID}.");

                if (artifact.LogCache is null) {
                    _services.Logger.Error($"Artifact '{artifactID}' has no log cache, this is unexpected!");
                    return;
                }

                _services.Logger.Debug($"Current context ID: {_services.Logger.LogContext.CurrentContextID}");
                artifact.LogCache.Replay(_services.Logger, _services.Logger.LogContext.CurrentContextID ?? Guid.Empty);
            } else if (res is BuildExitCodeFailed failed) {
                _services.Logger.Error($"Build failed for {project.Name} with artifact {artifactID}: {failed.Exception.Message}");
            }
        }
    }

    /// <summary>
    /// Resolves and runs the specified unresolved project.
    /// </summary>
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Exposed to JavaScript context")]
    public void run(UnresolvedProject project) {
        run(project.Resolve());
    }

    /// <summary>
    /// Runs the specified project using its language component.
    /// </summary>
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Exposed to JavaScript context")]
    public void run(Project project) {
        using (new ProfileScope(_services.Profiler, MethodBase.GetCurrentMethod()!)) {
            project.GetLanguageComponent().Run(project);
        }
    }

    /// <summary>
    /// Retrieves the value of the specified environment variable.
    /// </summary>
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Exposed to JavaScript context")]
    public string getEnv(string key, string? defaultValue = null) {
        return _context.GetEnvironmentVariable(key) ?? defaultValue ?? throw new Exception($"Environment variable {key} is not set.");
    }

    /// <summary>
    /// Retrieves the specified environment variable as a double or zero if unset.
    /// </summary>
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Exposed to JavaScript context")]
    public double getEnvNumber(string key, double? defaultValue = null) {
        var value = _context.GetEnvironmentVariable(key);
        return string.IsNullOrEmpty(value)
            ? 0
            : double.TryParse(value, out var result) ? result : defaultValue ?? throw new Exception($"Environment variable {key} is not a number: {value}");
    }

    /// <summary>
    /// Retrieves the specified environment variable as a boolean or false if unset.
    /// </summary>
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Exposed to JavaScript context")]
    public bool getEnvBool(string key, bool? defaultValue = null) {
        var value = _context.GetEnvironmentVariable(key);
        return string.IsNullOrEmpty(value)
            ? false
            : bool.TryParse(value, out var result) ? result : defaultValue ?? throw new Exception($"Environment variable {key} is not a number: {value}");
    }

    /// <summary>
    /// Sets the specified environment variable to the given value.
    /// </summary>
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Exposed to JavaScript context")]
    public void setEnv(string key, string value) {
        _context.SetEnvironmentVariable(key, value);
    }

    /// <summary>
    /// Imports an API type instance for the given key.
    /// </summary>
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Exposed to JavaScript context")]
    public object import(string key) {
        using (new ProfileScope(_services.Profiler, MethodBase.GetCurrentMethod()!)) {
            var t = Activator.CreateInstance(_services.ExtensionManager.GetAPIType(key));
            _services.Logger.Debug($"Importing {key} as {t}");

            if (t == null)
                throw new Exception($"Failed to import API type for key: {key}");

            return t;
        }
    }

    #region Job Actions

    /// <summary>
    /// Creates a shell-based job action with cmd.exe.
    /// </summary>
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Exposed to JavaScript context")]
    public IJobAction shell(string command) {
        return new JobShellAction(new(
            "cmd.exe",
            ["/c", .. command.Split(" ")]
        ));
    }

    /// <summary>
    /// Creates a job action to execute the given command.
    /// </summary>
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Exposed to JavaScript context")]
    public IJobAction execute(string command) {
        return new JobShellAction(new(
            command.Split(" ")[0],
            command.Split(" ")[1..]
        ));
    }

    #endregion
}
