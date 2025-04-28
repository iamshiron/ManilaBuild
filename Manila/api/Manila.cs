using Microsoft.ClearScript;
using Shiron.Manila.Ext;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Utils;

namespace Shiron.Manila.API;

// As class is exposed to the scripting environment, use JavaScript naming conventions
#pragma warning disable IDE1006

/// <summary>
/// The main Manila API class. Used for global functions.
/// </summary>
public sealed class Manila(ScriptContext context) : ExposedDynamicObject {
    private readonly ScriptContext Context = context;
    private readonly BuildConfig BuildConfig = new();

    /// <summary>
    /// Gets the current project in the Manila engine.
    /// </summary>
    /// <returns>The current project.</returns>
    /// <exception cref="Exception">Thrown when not in a project context.</exception>
    public Project getProject() {
        if (ManilaEngine.GetInstance().CurrentProject == null) throw new ContextException(Exceptions.Context.WORKSPACE, Exceptions.Context.PROJECT);
        return ManilaEngine.GetInstance().CurrentProject;
    }

    /// <summary>
    /// Gets an unresolved project with the specified name.
    /// </summary>
    /// <param name="name">The name of the project to get.</param>
    /// <returns>An unresolved project with the specified name.</returns>
    public UnresolvedProject getProject(string name) {
        return new UnresolvedProject(name);
    }

    /// <summary>
    /// Gets the workspace in the Manila engine.
    /// </summary>
    /// <returns>The workspace in the Manila engine.</returns>
    public Workspace getWorkspace() {
        return ManilaEngine.GetInstance().Workspace;
    }

    /// <summary>
    /// Gets the build configuration for this Manila instance.
    /// </summary>
    /// <returns>The build configuration for this Manila instance.</returns>
    public BuildConfig getConfig() {
        return BuildConfig;
    }

    /// <summary>
    /// Creates a new source set with the specified origin.
    /// </summary>
    /// <param name="origin">The origin of the source set.</param>
    /// <returns>A new source set with the specified origin.</returns>
    public SourceSet sourceSet(string origin) {
        return new SourceSet(origin);
    }

    /// <summary>
    /// Creates a new task with the specified name.
    /// </summary>
    /// <param name="name">The name of the task to create.</param>
    /// <returns>A new task with the specified name, associated with the current project and script context.</returns>
    public Task task(string name) {
        try {
            return new Task(name, getProject(), Context, Context.ScriptPath);
        } catch (ContextException e) {
            if (e.cIs != Exceptions.Context.WORKSPACE) throw;
            return new Task(name, getWorkspace(), Context, Context.ScriptPath);
        }
    }

    /// <summary>
    /// Creates a new directory reference with the specified path.
    /// </summary>
    /// <param name="path">The path of the directory.</param>
    /// <returns>A new directory reference with the specified path.</returns>
    public Dir dir(string path) {
        return new Dir(path);
    }

    /// <summary>
    /// Creates a new file reference with the specified path.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <returns>A new file reference with the specified path.</returns>
    public File file(string path) {
        return new File(path);
    }

    /// <summary>
    /// Applies the plugin component with the specified key to the current project.
    /// </summary>
    /// <param name="pluginComponentKey">The key of the plugin component to apply.</param>
    public void apply(string pluginComponentKey) {
        var component = ExtensionManager.GetInstance().GetPluginComponent(pluginComponentKey);
        apply(component);
    }

    /// <summary>
    /// Applies the plugin component specified by the script object to the current project.
    /// </summary>
    /// <param name="obj">A script object containing the group, name, component, and optional version of the plugin component to apply.</param>
    public void apply(ScriptObject obj) {
        var version = obj.GetProperty("version");
        var component = ExtensionManager.GetInstance().GetPluginComponent((string) obj["group"], (string) obj["name"], (string) obj["component"], version == Undefined.Value ? null : (string) version);
        apply(component);
    }

    /// <summary>
    /// Applies the specified plugin component to the current project.
    /// </summary>
    /// <param name="component">The plugin component to apply to the current project.</param>
    public void apply(PluginComponent component) {
        Logger.Debug("Applying: " + component);
        getProject().ApplyComponent(component);
    }

    /// <summary>
    /// Used for filtering projects and running actions on them.
    /// </summary>
    /// <param name="o">The type of filter, a subclass of <see cref="ProjectFilter"/></param>
    /// <param name="a">The action to run</param>
    public void onProject(object o, dynamic a) {
        var filter = ProjectFilter.From(o);
        getWorkspace().ProjectFilters.Add(new Tuple<ProjectFilter, Action<Project>>(filter, (project) => a(project)));
    }

    /// <summary>
    /// Runs a task with the specified key.
    /// </summary>
    /// <param name="key">The key</param>
    /// <exception cref="Exception">Thrown if task was not found</exception>
    public void runTask(string key) {
        var task = getWorkspace().GetTask(key);
        if (task == null) throw new Exception("Task not found: " + key);

        ApplicationLogger.TaskStarted(task);
        task.Action?.Invoke();
        ApplicationLogger.TaskFinished();
    }

    /// <summary>
    /// Calls the underlying compiler to build the project
    /// </summary>
    /// <param name="workspace">The workspace</param>
    /// <param name="project">The project</param>
    /// <param name="config">The config</param>
    public void build(Workspace workspace, Project project, BuildConfig config) {
        project.GetLanguageComponent().Build(workspace, project, config);
    }
    public void run(UnresolvedProject project) { run(project.Resolve()); }
    public void run(Project project) {
        project.GetLanguageComponent().Run(project);
    }
    public string getEnv(string key) {
        return Context.GetEnvironmentVariable(key);
    }
    public double getEnvNumber(string key) {
        var value = Context.GetEnvironmentVariable(key);
        if (string.IsNullOrEmpty(value)) return 0;
        if (double.TryParse(value, out var result)) return result;
        throw new Exception($"Environment variable {key} is not a number: {value}");
    }
    public bool getEnvBool(string key) {
        var value = Context.GetEnvironmentVariable(key);
        if (string.IsNullOrEmpty(value)) return false;
        if (bool.TryParse(value, out var result)) return result;
        throw new Exception($"Environment variable {key} is not a boolean: {value}");
    }
    public void setEnv(string key, string value) {
        Context.SetEnvironmentVariable(key, value);
    }

    public object import(string key) {
        var t = Activator.CreateInstance(ExtensionManager.GetInstance().GetAPIType(key));
        Logger.Debug($"Importing {key} as {t}");

        return t;
    }
}
