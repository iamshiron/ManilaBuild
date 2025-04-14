using Shiron.Manila;
using Shiron.Manila.Ext;
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
            ApplicationLogger.BuildStarted();
            var task = engine.Workspace.GetTask(arg);

            var order = task.GetExecutionOrder();
            Logger.Debug("Execution order: " + string.Join(", ", order));

            foreach (var t in order) {
                var taskToRun = engine.Workspace.GetTask(t);
                ApplicationLogger.TaskStarted(taskToRun);

                try {
                    if (taskToRun.Action == null) Logger.Warn("Task has no action: " + t);
                    else taskToRun.Action.Invoke();
                    ApplicationLogger.TaskFinished();
                } catch (Exception e) {
                    throw new TaskFailedException(taskToRun, e);
                }
            }

            ApplicationLogger.BuildFinished();
        } catch (TaskNotFoundException e) {
            ApplicationLogger.BuildFinished(e);
        } catch (TaskFailedException e) {
            ApplicationLogger.BuildFinished(e);
        } catch (Exception e) {
            ApplicationLogger.BuildFinished(e);
        }
    }
}
