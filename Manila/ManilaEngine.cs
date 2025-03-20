
using Shiron.Manila.Utils;

namespace Shiron.Manila;

public sealed class ManilaEngine {
	internal static readonly ManilaEngine instance = new ManilaEngine();
	public static ManilaEngine getInstance() { return instance; }


	public string root { get; private set; }
	public API.Workspace? workspace { get; }
	public API.Project? currentProject { get; private set; }
	public ScriptContext? currentContext { get; private set; }

	internal ManilaEngine() {
		root = Directory.GetCurrentDirectory();
		workspace = new API.Workspace(root);
	}

	public void run() {
		if (!File.Exists("Manila.js")) {
			Logger.error("No Manila.js file found in the current directory.");
			return;
		}

		var workspaceScript = Path.Join(root, "Manila.js");
		var files = Directory.GetFiles(".", "Manila.js", SearchOption.AllDirectories)
			.Where(f => !Path.GetFullPath(f).Equals(Path.GetFullPath("Manila.js")))
			.ToList();

		foreach (var script in files) {
			runScript(script);
		}
		runScript(workspaceScript);
	}

	public void runScript(string path) {
		Logger.debug("Running script: " + path);
		string projectPath = Path.GetDirectoryName(Path.GetRelativePath(root, path));
		string name = projectPath.ToLower().Replace(Path.DirectorySeparatorChar, ':');

		currentProject = new API.Project(name, projectPath);
		workspace!.projects.Add(name, currentProject);

		currentContext = new ScriptContext(this, currentProject, path);
		currentContext.init();
		currentContext.execute();

		currentProject = null;
		currentContext = null;
	}
}
