using Microsoft.ClearScript;
using Shiron.Manila.Utils;

namespace Shiron.Manila.API;

/// <summary>
/// The main Manila API class. Used for global functions.
/// </summary>
public sealed class Manila {
	private ScriptContext context;


	public Manila(ScriptContext context) {
		this.context = context;
	}

	public void apply(string identifier) {
		Logger.debug("Applying: " + identifier);
	}
	public void apply(ScriptObject obj) {
		Logger.debug("Applying: " + obj);
	}
	public void apply(string group, string name, string version, string component) {
		Logger.debug("Applying: " + group + ":" + name + "@" + version + ":" + component + ":");
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
		return new File();
	}
}
