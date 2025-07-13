
using Shiron.Manila.Exceptions;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shiron.Manila.CLI.Commands;

internal sealed class ArtifactsCommand : Command<ArtifactsCommand.Settings> {
    public sealed class Settings : DefaultCommandSettings { }

    public override int Execute(CommandContext context, Settings settings) {
        var engine = ManilaEngine.GetInstance();

        ManilaCLI.InitExtensions();
        engine.Run().Wait();
        if (engine.Workspace == null) throw new ManilaException("Not inside a workspace");

        AnsiConsole.Write(new Rule("[bold yellow]Available Artifacts[/]").RuleStyle("grey").DoubleBorder());

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn(new TableColumn("[blue]Artifact[/]"));
        table.AddColumn(new TableColumn("[cyan]Project[/]"));
        table.AddColumn(new TableColumn("[green]Description[/]"));
        table.AddColumn(new TableColumn("[blue]Tasks[/]"));

        foreach (var p in engine.Workspace.Projects) {
            var project = p.Value;

            foreach (var (name, artifact) in project.Artifacts) {
                var artifactsTable = new Table();
                artifactsTable.AddColumn(new TableColumn("[cyan]Task[/]"));
                artifactsTable.AddColumn(new TableColumn("[green]Description[/]"));

                foreach (var task in artifact.Tasks) {
                    artifactsTable.AddRow($"[cyan bold]{task.Name}[/]", task.Description);
                }

                table.AddRow(
                    new Markup($"[bold cyan]{project.Name}[/]"),
                    new Markup($"[bold blue]{name}[/]"),
                    new Markup(artifact.Description ?? ""),
                    artifactsTable
                );
            }
        }

        AnsiConsole.Write(table);
        return 0;
    }
}
