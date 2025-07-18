using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.ClearScript;
using Shiron.Manila.API.Builders;
using Shiron.Manila.Artifacts;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Ext;
using Shiron.Manila.Logging;
using Shiron.Manila.Profiling;
using Shiron.Manila.Utils;

namespace Shiron.Manila.API;

/// <summary>
/// Primary API class exposing global Manila functions.
/// </summary>
public sealed class Manila(ScriptContext context) : ExposedDynamicObject {
    private readonly ScriptContext _context = context;

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
    public Project getProject() {
        if (ManilaEngine.GetInstance().CurrentProject == null) throw new ContextException(Exceptions.Context.WORKSPACE, Exceptions.Context.PROJECT);
        return ManilaEngine.GetInstance().CurrentProject!;
    }

    /// <summary>
    /// Creates an unresolved project reference by name.
    /// </summary>
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Exposed to JavaScript context")]
    public UnresolvedProject getProject(string name) {
        return new UnresolvedProject(name);
    }

    /// <summary>
    /// Gets the current Manila workspace.
    /// </summary>
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Exposed to JavaScript context")]
    public Workspace getWorkspace() {
        return ManilaEngine.GetInstance().Workspace;
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
        if (BuildConfig == null) throw new ManilaException("Cannot apply artifact when no language has been applied!");
        var builder = new ArtifactBuilder(() => lambda(), this, (BuildConfig) BuildConfig, getProject().Name);
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
        if (CurrentArtifactBuilder != null) {
            var jobBuilder = new JobBuilder(name, _context, getProject(), CurrentArtifactBuilder);
            CurrentArtifactBuilder.JobBuilders.Add(jobBuilder);
            return jobBuilder;
        }

        try {
            var builder = new JobBuilder(name, _context, getProject(), null);
            JobBuilders.Add(builder);
            return builder;
        } catch (ContextException e) {
            if (e.Is != Exceptions.Context.WORKSPACE) throw;
            var builder = new JobBuilder(name, _context, getWorkspace(), null);
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
        var component = ExtensionManager.GetInstance().GetPluginComponent(pluginComponentKey);
        apply(component);
    }

    /// <summary>
    /// Applies the plugin component defined in the script object.
    /// </summary>
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Exposed to JavaScript context")]
    public void apply(ScriptObject obj) {
        var version = obj.GetProperty("version");
        var component = ExtensionManager.GetInstance().GetPluginComponent((string) obj["group"], (string) obj["name"], (string) obj["component"], version == Undefined.Value ? null : (string) version);
        apply(component);
    }

    /// <summary>
    /// Applies the provided plugin component to the current project.
    /// </summary>
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Exposed to JavaScript context")]
    public void apply(PluginComponent component) {
        using (new ProfileScope(MethodBase.GetCurrentMethod()!)) {
            Logger.Debug("Applying: " + component);
            getProject().ApplyComponent(component);
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
        using (new ProfileScope(MethodBase.GetCurrentMethod()!)) {
            var filter = ProjectFilter.From(o);
            getWorkspace().ProjectFilters.Add(new Tuple<ProjectFilter, Action<Project>>(filter, (project) => a(project)));
        }
    }

    /// <summary>
    /// Executes the job identified by the given key.
    /// </summary>
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Exposed to JavaScript context")]
    public async Task runJob(string key) {
        var job = ManilaEngine.GetInstance().GetJob(key) ?? throw new Exception("Job not found: " + key);
        await job.Execute();
    }

    /// <summary>
    /// Builds the project using its language component.
    /// </summary>
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Exposed to JavaScript context")]
    public void build(Workspace workspace, Project project, BuildConfig config, string artifactID) {
        using (new ProfileScope(MethodBase.GetCurrentMethod()!)) {
            project.GetLanguageComponent().Build(workspace, project, config, project.Artifacts[artifactID]);
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
        using (new ProfileScope(MethodBase.GetCurrentMethod()!)) {
            project.GetLanguageComponent().Run(project);
        }
    }

    /// <summary>
    /// Retrieves the value of the specified environment variable.
    /// </summary>
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Exposed to JavaScript context")]
    public string getEnv(string key) {
        return _context.GetEnvironmentVariable(key);
    }

    /// <summary>
    /// Retrieves the specified environment variable as a double or zero if unset.
    /// </summary>
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Exposed to JavaScript context")]
    public double getEnvNumber(string key) {
        var value = _context.GetEnvironmentVariable(key);
        if (string.IsNullOrEmpty(value)) return 0;
        if (double.TryParse(value, out var result)) return result;
        throw new Exception($"Environment variable {key} is not a number: {value}");
    }

    /// <summary>
    /// Retrieves the specified environment variable as a boolean or false if unset.
    /// </summary>
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Exposed to JavaScript context")]
    public bool getEnvBool(string key) {
        var value = _context.GetEnvironmentVariable(key);
        if (string.IsNullOrEmpty(value)) return false;
        if (bool.TryParse(value, out var result)) return result;
        throw new Exception($"Environment variable {key} is not a boolean: {value}");
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
        using (new ProfileScope(MethodBase.GetCurrentMethod()!)) {
            var t = Activator.CreateInstance(ExtensionManager.GetInstance().GetAPIType(key));
            Logger.Debug($"Importing {key} as {t}");

            if (t == null)
                throw new Exception($"Failed to import API type for key: {key}");

            return t;
        }
    }

    // Job Actions
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
}
