
using Shiron.Manila.API;
using Shiron.Manila.Attributes;
using Shiron.Manila.Utils;

namespace Shiron.Manila;

public sealed class ManilaEngine {
	internal static ManilaEngine? instance = null;
	public static ManilaEngine getInstance() { if (instance == null) instance = new ManilaEngine(); return instance; }

	public string root { get; private set; }
	public API.Workspace? workspace { get; }
	public API.Project? currentProject { get; private set; }
	public ScriptContext? currentContext { get; private set; }
	public ScriptContext workspaceContext { get; }

	internal ManilaEngine() {
		root = Directory.GetCurrentDirectory();
		workspace = new API.Workspace(root);
		workspaceContext = new ScriptContext(this, workspace, Path.Join(root, "Manila.js"));
	}

	public void run() {
		if (!System.IO.File.Exists("Manila.js")) {
			Logger.error("No Manila.js file found in the current directory.");
			return;
		}

		var workspaceScript = Path.Join(root, "Manila.js");
		var files = Directory.GetFiles(".", "Manila.js", SearchOption.AllDirectories)
			.Where(f => !Path.GetFullPath(f).Equals(Path.GetFullPath("Manila.js")))
			.ToList();

		runWorkspaceScript();
		foreach (var script in files) {
			runProjectScript(script);
		}

		foreach (var f in workspace!.projectFilters) {
			foreach (var p in workspace.projects.Values) {
				if (f.Item1.predicate(p)) {
					foreach (var type in p.plugins) {
						var plugin = ExtensionManager.getInstance().getPlugin(type);
						foreach (var e in plugin.enums) {
							// Might apply enums multiple times, but that's fine as it's already checked in the applyEnum method
							workspaceContext.applyEnum(e);
						}
					}
					f.Item2.Invoke(p);
				}
			}
		}
	}

	public void runProjectScript(string path) {
		Logger.debug("Running project script: " + path);
		string projectPath = Path.GetDirectoryName(Path.GetRelativePath(root, path));
		string name = projectPath.ToLower().Replace(Path.DirectorySeparatorChar, ':');

		currentProject = new API.Project(name, projectPath);
		workspace!.projects.Add(name, currentProject);
		currentContext = new ScriptContext(this, currentProject, Path.Join(root, path));

		currentContext.applyEnum(typeof(EPlatform));
		currentContext.applyEnum(typeof(EArchitecture));

		currentContext.init();
		currentContext.execute();

		currentProject = null;
		currentContext = null;
	}
	public void runWorkspaceScript() {
		string path = "Manila.js";
		Logger.debug("Running workspace script: " + path);

		workspaceContext.applyEnum(typeof(EPlatform));
		workspaceContext.applyEnum(typeof(EArchitecture));

		workspaceContext.init();
		workspaceContext.execute();
	}
}
