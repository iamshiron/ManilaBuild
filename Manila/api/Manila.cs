using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;
using Shiron.Manila.Ext;
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

	public Project getProject() {
		if (ManilaEngine.getInstance().currentProject == null) throw new Exception("Not in project context.");
		return ManilaEngine.getInstance().currentProject;
	}
	public UnresolvedProject getProject(string name) {
		return new UnresolvedProject(name);
	}

	public Workspace getWorkspace() {
		return ManilaEngine.getInstance().workspace;
	}

	public BuildConfig getConfig() {
		return buildConfig;
	}

	public SourceSet sourceSet(string origin) {
		return new SourceSet();
	}
	public Task task(string name) {
		return new Task(name, ManilaEngine.getInstance().currentProject, context);
	}
	public Dir dir(string path) {
		return new Dir(path);
	}
	public File file(string path) {
		return new File(path);
	}

	public void import(string pluginKey) {
		var plugin = ExtensionManager.getInstance().getPlugin(pluginKey);
		import(plugin);
	}
	public void import(ScriptObject obj) {
		var plugin = ExtensionManager.getInstance().getPlugin((string) obj["group"], (string) obj["name"], (string) obj["version"]);
		import(plugin);
	}
	public void import(ManilaPlugin plugin) {
		Logger.debug("Importing: " + plugin);
	}

	public void apply(string pluginComponentKey) {
		var component = ExtensionManager.getInstance().getPluginComponent(pluginComponentKey);
		apply(component);
	}
	public void apply(ScriptObject obj) {
		var version = obj.GetProperty("version");
		var component = ExtensionManager.getInstance().getPluginComponent((string) obj["group"], (string) obj["name"], (string) obj["component"], version == Undefined.Value ? null : (string) version);
		apply(component);
	}
	public void apply(PluginComponent component) {
		Logger.debug("Applying: " + component);
		getProject().applyComponent(component);
	}
}
