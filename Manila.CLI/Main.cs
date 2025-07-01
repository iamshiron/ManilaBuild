using Shiron.Manila;
using Shiron.Manila.Ext;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Logging;
using Spectre.Console;
using Shiron.Manila.API;

Directory.SetCurrentDirectory("./run");
var startTime = DateTime.Now.Ticks;

var logOptions = new {
    Structured = args.Contains("--structured") || args.Contains("--json"),
    Verbose = args.Contains("--verbose"),
    Quiet = args.Contains("--quiet"),
    StackTrace = args.Contains("--stack-trace")
};

if (!logOptions.Quiet) {
    AnsiConsole.MarkupLine(@"[blue] __  __             _ _[/]");
    AnsiConsole.MarkupLine(@"[blue]|  \/  | __ _ _ __ (_| | __ _[/]");
    AnsiConsole.MarkupLine(@"[blue]| |\/| |/ _` | '_ \| | |/ _` |[/]");
    AnsiConsole.MarkupLine(@"[blue]| |  | | (_| | | | | | | (_| |[/]");
    AnsiConsole.MarkupLine($"[blue]|_|  |_|\\__,_|_| |_|_|_|\\__,_|[/] [magenta]v{ManilaEngine.VERSION}[/]\n");
}

var engine = ManilaEngine.GetInstance();

var extensionManager = ExtensionManager.GetInstance();

StdOutSink.Init(logOptions.Verbose, logOptions.Quiet, logOptions.Structured);

extensionManager.Init("./.manila/plugins");
extensionManager.LoadPlugins();
extensionManager.InitPlugins();

Console.WriteLine("Initializing...");
engine.Run();

if (engine.Workspace == null) throw new Exception("Workspace not found");
Console.WriteLine("Initialization took: " + (DateTime.Now.Ticks - startTime) / TimeSpan.TicksPerMillisecond + "ms\n");
foreach (var arg in args) {
    if (arg.StartsWith(":")) {
        try {
            var task = engine.Workspace.GetTask(arg);

            var order = task.GetExecutionOrder();
            Logger.Debug("Execution order: " + string.Join(", ", order));

            foreach (var t in order) {
                var taskToRun = engine.Workspace.GetTask(t);

                try {
                    if (taskToRun.Action == null) Logger.Warning("Task has no action: " + t);
                    else taskToRun.Action.Invoke();
                } catch (Exception e) {
                    throw new TaskFailedException(taskToRun, e);
                }
            }

        } catch (Exception e) {
            Console.WriteLine(e);
        }

        extensionManager.ReleasePlugins();
        return;
    } else {
        if (arg == "tasks") {
            AnsiConsole.Write(new Rule("[bold yellow]Available Tasks[/]").RuleStyle("grey").DoubleBorder());

            var workspaceTable = new Table().Border(TableBorder.Rounded);
            workspaceTable.AddColumn(new TableColumn("[cyan]Task[/]"));
            workspaceTable.AddColumn(new TableColumn("[green]Description[/]"));
            workspaceTable.AddColumn(new TableColumn("[magenta]Direct Dependencies[/]"));

            foreach (var t in engine.Workspace.Tasks) {
                workspaceTable.AddRow(
                    $"[bold cyan]{t.GetIdentifier()}[/]",
                    t.Description ?? "",
                    t.dependencies.Count > 0 ? $"[italic]{string.Join(", ", t.dependencies)}[/]" : "");
            }

            AnsiConsole.MarkupLine("\n[bold blue]Workspace Tasks[/]");
            AnsiConsole.Write(workspaceTable);

            foreach (var p in engine.Workspace.Projects) {
                var project = p.Value;

                var projectTable = new Table().Border(TableBorder.Rounded);
                projectTable.AddColumn(new TableColumn("[cyan]Task[/]"));
                projectTable.AddColumn(new TableColumn("[green]Description[/]"));
                projectTable.AddColumn(new TableColumn("[magenta]Direct Dependencies[/]"));


                foreach (var t in project.Tasks) {
                    projectTable.AddRow(
                        $"[bold cyan]{t.GetIdentifier()}[/]",
                        t.Description ?? "",
                        t.dependencies.Count > 0 ? $"[italic]{string.Join(", ", t.dependencies)}[/]" : "");
                }

                AnsiConsole.MarkupLine($"\n[bold blue]{project.Name}[/]");
                AnsiConsole.Write(projectTable);
            }
        } else if (arg == "plugins") {
            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn(new TableColumn("[cyan]Plugin[/]"));
            table.AddColumn(new TableColumn("[green]Version[/]"));
            table.AddColumn(new TableColumn("[magenta]Group[/]"));
            table.AddColumn(new TableColumn("[yellow]Path[/]"));
            table.AddColumn(new TableColumn("[red]Author[/]"));
            foreach (var p in ExtensionManager.GetInstance().Plugins) {
                table.AddRow(
                    $"[bold cyan]{p.Name}[/]",
                    p.Version.ToString(),
                    p.Group ?? "",
                    Path.GetFileName(p.File) ?? "",
                    p.Authors.Count > 0 ? Markup.Escape($"{string.Join(", ", p.Authors)}") : "");
            }

            AnsiConsole.Write(new Rule("[bold yellow]Available Plugins[/]\n").RuleStyle("grey").DoubleBorder());
            AnsiConsole.Write(table);

            extensionManager.ReleasePlugins();
        } else {
            AnsiConsole.MarkupLine($"[red]Unknown command: {arg}[/]");
            AnsiConsole.MarkupLine("[yellow]Available commands: tasks, plugins[/]");

            extensionManager.ReleasePlugins();
        }

        return;
    }
}
