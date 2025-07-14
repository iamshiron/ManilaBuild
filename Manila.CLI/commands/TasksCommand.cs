using Shiron.Manila.Exceptions;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shiron.Manila.CLI.Commands;

internal sealed class TasksCommand : BaseAsyncManilaCommand<TasksCommand.Settings> {
    public sealed class Settings : DefaultCommandSettings { }

    protected override async System.Threading.Tasks.Task<int> ExecuteCommandAsync(CommandContext context, Settings settings) {
        var engine = ManilaEngine.GetInstance();

        ManilaCLI.InitExtensions();
        await engine.Run();
        if (engine.Workspace == null) throw new ManilaException("Not inside a workspace");

        AnsiConsole.Write(new Rule("[bold yellow]Available Tasks[/]").RuleStyle("grey").DoubleBorder());

        if (engine.Workspace.Tasks.Count > 0) {
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
        }

        foreach (var p in engine.Workspace.Projects) {
            var project = p.Value;

            // Only show project table if it has tasks
            if (project.Tasks.Count > 0) {
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

            // Display artifact tasks
            foreach (var artifact in project.Artifacts) {
                if (artifact.Value.Tasks.Length == 0) continue;

                var artifactTable = new Table().Border(TableBorder.Rounded);
                artifactTable.AddColumn(new TableColumn("[cyan]Task[/]"));
                artifactTable.AddColumn(new TableColumn("[green]Description[/]"));
                artifactTable.AddColumn(new TableColumn("[magenta]Direct Dependencies[/]"));

                foreach (var t in artifact.Value.Tasks) {
                    artifactTable.AddRow(
                        $"[bold cyan]{t.GetIdentifier()}[/]",
                        t.Description ?? "",
                        t.Dependencies.Count > 0 ? $"[italic]{string.Join(", ", t.Dependencies)}[/]" : "");
                }

                AnsiConsole.MarkupLine($"\n[bold yellow]{project.Name} → {artifact.Key}[/]");
                AnsiConsole.Write(artifactTable);
            }
        }

        return ExitCodes.SUCCESS;
    }
}
