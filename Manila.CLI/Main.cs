using Shiron.Manila;
using Shiron.Manila.API;
using Shiron.Manila.Attributes;
using Shiron.Manila.Utils;

#if DEBUG
Directory.SetCurrentDirectory("E:/dev/Manila./run");
#endif

Logger.init(true, false);

var engine = ManilaEngine.getInstance();
var extensionManager = ExtensionManager.getInstance();

extensionManager.init("./.manila/plugins");
extensionManager.loadPlugins();
extensionManager.initPlugins();

engine.run();
extensionManager.releasePlugins();

if (engine.workspace == null) throw new Exception("Workspace not found");
foreach (var arg in args) {
	if (arg.StartsWith(":")) {
		var task = engine.workspace.getTask(arg[1..]);
		if (task == null) throw new Exception("Task not found: " + arg[1..]);

		var order = task.getExecutionOrder();
		Logger.debug("Execution order: " + string.Join(", ", order));

		foreach (var t in order) {
			var taskToRun = engine.workspace.getTask(t);
			if (taskToRun == null) throw new Exception("Task not found: " + t);
			if (taskToRun.action == null) Logger.warn("Task has no action: " + t);
			else taskToRun.action.Invoke();
		}
	}
}
