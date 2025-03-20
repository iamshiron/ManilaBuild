using Shiron.Manila.API;

namespace Shiron.Manila;

public sealed class ManilaEngine {
	internal static readonly ManilaEngine instance = new ManilaEngine();
	public string root { get; private set; }

	public static ManilaEngine getInstance() { return instance; }

	internal ManilaEngine() {
		root = Directory.GetCurrentDirectory();
	}

	public void runScript(string path) {
		Console.WriteLine("Running script: " + path);
		string projectPath = Path.GetDirectoryName(Path.GetRelativePath(root, path));
		string name = projectPath.ToLower().Replace(Path.DirectorySeparatorChar, ':');

		ScriptContext context = new ScriptContext(this, path);
		context.init();
		context.execute();
	}
}
