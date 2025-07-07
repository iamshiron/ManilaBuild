
using Shiron.Manila;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Ext;
using Shiron.Manila.Profiling;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shiron.Manila.CLI.Commands;

internal sealed class TasksCommand : Command<TasksCommand.Settings> {
    public sealed class Settings : DefaultCommandSettings { }

    public override int Execute(CommandContext context, Settings settings) {
        var engine = ManilaEngine.GetInstance();

        ManilaCLI.InitExtensions();
        engine.Run().Wait();
        if (engine.Workspace == null) throw new ManilaException("Not inside a workspace");

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

        return 0;
    }
}
