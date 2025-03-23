using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;
using Shiron.Manila.Attributes;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Utils;

namespace Shiron.Manila.API;

/// <summary>
/// The main Manila API class. Used for global functions.
/// </summary>
public sealed class Manila {
	private ScriptContext context;
	private BuildConfig buildConfig = new BuildConfig();

	public Manila(ScriptContext context) {
		this.context = context;
	}

	/// <summary>
	/// Gets the current project in the Manila engine.
	/// </summary>
	/// <returns>The current project.</returns>
	/// <exception cref="Exception">Thrown when not in a project context.</exception>
	public Project getProject() {
		if (ManilaEngine.getInstance().currentProject == null) throw new ContextException(Context.WORKSPACE, Context.PROJECT);
		return ManilaEngine.getInstance().currentProject;
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
		return ManilaEngine.getInstance().workspace;
	}

	/// <summary>
	/// Gets the build configuration for this Manila instance.
	/// </summary>
	/// <returns>The build configuration for this Manila instance.</returns>
	public BuildConfig getConfig() {
		return buildConfig;
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
			return new Task(name, getProject(), context);
		} catch (ContextException e) {
			if (e.cIs != Context.WORKSPACE) throw;
			return new Task(name, getWorkspace(), context);
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
	/// Imports the plugin with the specified key.
	/// </summary>
	/// <param name="pluginKey">The key of the plugin to import.</param>
	public void import(string pluginKey) {
		var plugin = ExtensionManager.getInstance().getPlugin(pluginKey);
		import(plugin);
	}

	/// <summary>
	/// Imports the plugin specified by the script object.
	/// </summary>
	/// <param name="obj">A script object containing the group, name, and version of the plugin to import.</param>
	public void import(ScriptObject obj) {
		var plugin = ExtensionManager.getInstance().getPlugin((string) obj["group"], (string) obj["name"], (string) obj["version"]);
		import(plugin);
	}

	/// <summary>
	/// Imports the specified Manila plugin.
	/// </summary>
	/// <param name="plugin">The Manila plugin to import.</param>
	public void import(ManilaPlugin plugin) {
		Logger.debug("Importing: " + plugin);
	}

	/// <summary>
	/// Applies the plugin component with the specified key to the current project.
	/// </summary>
	/// <param name="pluginComponentKey">The key of the plugin component to apply.</param>
	public void apply(string pluginComponentKey) {
		var component = ExtensionManager.getInstance().getPluginComponent(pluginComponentKey);
		apply(component);
	}

	/// <summary>
	/// Applies the plugin component specified by the script object to the current project.
	/// </summary>
	/// <param name="obj">A script object containing the group, name, component, and optional version of the plugin component to apply.</param>
	public void apply(ScriptObject obj) {
		var version = obj.GetProperty("version");
		var component = ExtensionManager.getInstance().getPluginComponent((string) obj["group"], (string) obj["name"], (string) obj["component"], version == Undefined.Value ? null : (string) version);
		apply(component);
	}

	/// <summary>
	/// Applies the specified plugin component to the current project.
	/// </summary>
	/// <param name="component">The plugin component to apply to the current project.</param>
	public void apply(PluginComponent component) {
		Logger.debug("Applying: " + component);
		getProject().applyComponent(component);
	}

	public void project(object o, dynamic a) {
		var filter = ProjectFilter.from(o);
		getWorkspace().projectFilters.Add(new Tuple<ProjectFilter, Action<Project>>(filter, (project) => a(project)));
	}
}
