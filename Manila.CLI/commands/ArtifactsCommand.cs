
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
        table.AddColumn(new TableColumn("[cyan]Artifact[/]"));
        table.AddColumn(new TableColumn("[blue]Project[/]"));
        table.AddColumn(new TableColumn("[green]Description[/]"));

        foreach (var p in engine.Workspace.Projects) {
            var project = p.Value;

            foreach (var (name, artifact) in project._artifacs) {
                table.AddRow(
                    $"[bold cyan]{name}[/]",
                    $"[bold blue]{project.Name}[/]",
                    artifact.Description ?? "");
            }
        }

        AnsiConsole.Write(table);
        return 0;
    }
}
