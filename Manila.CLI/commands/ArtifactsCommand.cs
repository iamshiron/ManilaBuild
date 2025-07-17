
using Shiron.Manila.Exceptions;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shiron.Manila.CLI.Commands;

internal sealed class ArtifactsCommand : AsyncCommand<ArtifactsCommand.Settings> {
    public sealed class Settings : DefaultCommandSettings { }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings) {
        var engine = ManilaEngine.GetInstance();

        ManilaCLI.InitExtensions();
        await engine.Run();
        if (engine.Workspace == null) throw new ManilaException("Not inside a workspace");

        AnsiConsole.Write(new Rule("[bold yellow]Available Artifacts[/]").RuleStyle("grey").DoubleBorder());

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn(new TableColumn("[blue]Artifact[/]"));
        table.AddColumn(new TableColumn("[cyan]Project[/]"));
        table.AddColumn(new TableColumn("[green]Description[/]"));
        table.AddColumn(new TableColumn("[blue]Jobs[/]"));

        foreach (var p in engine.Workspace.Projects) {
            var project = p.Value;

            foreach (var (name, artifact) in project.Artifacts) {
                var artifactsTable = new Table();
                artifactsTable.AddColumn(new TableColumn("[cyan]Job[/]"));
                artifactsTable.AddColumn(new TableColumn("[green]Description[/]"));

                foreach (var job in artifact.Jobs) {
                    artifactsTable.AddRow($"[cyan bold]{job.Name}[/]", job.Description);
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
        return ExitCodes.SUCCESS;
    }
}
