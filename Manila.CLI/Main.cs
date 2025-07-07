using Shiron.Manila;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Ext;
using Shiron.Manila.CLI;
using Spectre.Console;
using Shiron.Manila.Profiling;

#if DEBUG
Directory.SetCurrentDirectory("E:\\dev\\Manila\\manila\\run");
Profiler.IsEnabled = true;
#endif

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

AnsiConsoleRenderer.Init(logOptions.Quiet, logOptions.Verbose, logOptions.Structured, logOptions.StackTrace);

using (new ProfileScope("Initializing Plugins")) {
    extensionManager.Init("./.manila/plugins");
    extensionManager.LoadPlugins();
    extensionManager.InitPlugins();
}

try {
    using (new ProfileScope("Running Engine")) {
        await engine.Run();
    }

    if (engine.Workspace == null) throw new Exception("Workspace not found");
    foreach (var arg in args) {
        if (arg.StartsWith(":")) {
            using (new ProfileScope("Executing Build Logic")) {
                engine.ExecuteBuildLogic(arg[1..]);
            }
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
                        t.Dependencies.Count > 0 ? $"[italic]{string.Join(", ", t.Dependencies)}[/]" : "");
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
                            t.Dependencies.Count > 0 ? $"[italic]{string.Join(", ", t.Dependencies)}[/]" : "");
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
        }
    }
} catch (ScriptingException e) {
    //  scripting errors are common user-facing issues.
    AnsiConsole.MarkupLine($"\n[red]{Emoji.Known.CrossMark} Script Error:[/] [white]{Markup.Escape(e.Message)}[/]");
    AnsiConsole.MarkupLine("[grey]This error occurred while executing a script. Check the script for syntax errors or logic issues.[/]");
    AnsiConsole.MarkupLine("[grey]Run with --stack-trace for a detailed technical log.[/]");
    if (logOptions.StackTrace) Utils.TryWriteException(e.InnerException ?? e);

    return;
} catch (BuildException e) {
    // build errors indicate a failure in the compilation or packaging process.
    AnsiConsole.MarkupLine($"\n[red]{Emoji.Known.CrossMark} Build Error:[/] [white]{e.Message}[/]");
    AnsiConsole.MarkupLine("[grey]The project failed to build. Review the build configuration and source files for errors.[/]");
    AnsiConsole.MarkupLine("[grey]Run with --stack-trace for a detailed technical log.[/]");
    if (logOptions.StackTrace) Utils.TryWriteException(e.InnerException ?? e);

    return;
} catch (ConfigurationException e) {
    // configuration errors are often due to invalid settings.
    AnsiConsole.MarkupLine($"\n[yellow]{Emoji.Known.Warning} Configuration Error:[/] [white]{e.Message}[/]");
    AnsiConsole.MarkupLine("[grey]There is a problem with a configuration file or setting. Please verify it is correct.[/]");
    AnsiConsole.MarkupLine("[grey]Run with --stack-trace for more technical details.[/]");
    if (logOptions.StackTrace) Utils.TryWriteException(e.InnerException ?? e);

    return;
} catch (ManilaException e) {
    // this is a known, handled application error.
    AnsiConsole.MarkupLine($"\n[yellow]{Emoji.Known.Warning} Application Error:[/] [white]{e.Message}[/]");
    AnsiConsole.MarkupLine($"[grey]A known issue ('{e.GetType().Name}') occurred. This is a handled error condition.[/]");
    AnsiConsole.MarkupLine("[grey]Run with --stack-trace for more technical details.[/]");
    if (logOptions.StackTrace) Utils.TryWriteException(e);

    return;
} catch (Exception e) {
    // this is a critical, unexpected error that likely indicates a bug.
    AnsiConsole.MarkupLine($"\n[red]{Emoji.Known.Collision} Unexpected System Exception:[/] [white]{e.GetType().Name}[/]");
    AnsiConsole.MarkupLine("[red]This may indicate a bug in the application. Please report this issue.[/]");
    AnsiConsole.MarkupLine("[grey]Run with --stack-trace for a detailed error log.[/]");
    if (logOptions.StackTrace) Utils.TryWriteException(e);

    return;
}

extensionManager.ReleasePlugins();
Profiler.SaveToFile(Path.Combine(ManilaEngine.GetInstance().DataDir, "profiling"));
