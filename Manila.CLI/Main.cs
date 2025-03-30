using Shiron.Manila;
using Shiron.Manila.Attributes;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Utils;
using Spectre.Console;

#if DEBUG
Directory.SetCurrentDirectory("E:/dev/Manila./run");
#endif

var verbose = args.Contains("--verbose") || args.Contains("-v");
var stackTrace = args.Contains("--stack-trace");
var quiet = args.Contains("--quiet") || args.Contains("-q");

if (!quiet) {
    AnsiConsole.MarkupLine(@"[blue] __  __             _ _[/]");
    AnsiConsole.MarkupLine(@"[blue]|  \/  | __ _ _ __ (_| | __ _[/]");
    AnsiConsole.MarkupLine(@"[blue]| |\/| |/ _` | '_ \| | |/ _` |[/]");
    AnsiConsole.MarkupLine(@"[blue]| |  | | (_| | | | | | | (_| |[/]");
    AnsiConsole.MarkupLine($"[blue]|_|  |_|\\__,_|_| |_|_|_|\\__,_|[/] [magenta]v{ManilaEngine.VERSION}[/]\n");
}

Logger.Init(verbose, quiet);
ApplicationLogger.Init(quiet, stackTrace);

var engine = ManilaEngine.GetInstance();
var extensionManager = ExtensionManager.GetInstance();

extensionManager.Init("./.manila/plugins");
extensionManager.LoadPlugins();
extensionManager.InitPlugins();

engine.Run();
extensionManager.ReleasePlugins();

if (engine.Workspace == null) throw new Exception("Workspace not found");
foreach (var arg in args) {
    if (arg.StartsWith(":")) {
        try {
            var task = engine.Workspace.GetTask(arg) ?? throw new Exception("Task not found: " + arg[1..]);
            var order = task.GetExecutionOrder();
            Logger.debug("Execution order: " + string.Join(", ", order));

            ApplicationLogger.BuildStarted();
            foreach (var t in order) {
                var taskToRun = engine.Workspace.GetTask(t) ?? throw new Exception("Task not found: " + t);
                ApplicationLogger.TaskStarted(taskToRun);

                try {
                    if (taskToRun.Action == null) Logger.warn("Task has no action: " + t);
                    else taskToRun.Action.Invoke();
                    ApplicationLogger.TaskFinished();
                } catch (Exception e) {
                    throw new TaskFailedException(taskToRun, e);
                }
            }

            ApplicationLogger.BuildFinished();
        } catch (Exception e) {
            ApplicationLogger.BuildFinished(e.InnerException);
            Console.WriteLine(e);
        }
    }
}
