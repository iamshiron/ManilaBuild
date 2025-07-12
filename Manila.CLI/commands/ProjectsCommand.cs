using Shiron.Manila.Exceptions;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shiron.Manila.CLI.Commands;

internal sealed class ProjectsCommand : Command<ProjectsCommand.Settings> {
    public sealed class Settings : DefaultCommandSettings { }

    public override int Execute(CommandContext context, Settings settings) {
        var engine = ManilaEngine.GetInstance();

        ManilaCLI.InitExtensions();
        engine.Run().Wait();
        if (engine.Workspace == null) throw new ManilaException("Not inside a workspace");

        AnsiConsole.Write(new Rule("[bold yellow]Available Projects[/]").RuleStyle("grey").DoubleBorder());

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn(new TableColumn("[cyan]Project[/]"));
        table.AddColumn(new TableColumn("[green]Description[/]"));
        table.AddColumn(new TableColumn("[magenta]Version[/]"));
        table.AddColumn(new TableColumn("[blue]Artifacts[/]"));

        foreach (var p in engine.Workspace.Projects) {
            var project = p.Value;
            var artifactsTable = new Table().Border(TableBorder.Rounded);
            artifactsTable.AddColumn(new TableColumn("[cyan]Artifact[/]"));
            artifactsTable.AddColumn(new TableColumn("[green]Description[/]"));

            foreach (var (name, artifact) in project._artifacs) {
                artifactsTable.AddRow($"[bold cyan]{name}[/]", artifact.Description);
            }

            table.AddRow(new Markup($"[cyan bold]{project.Name}[/]"), new Markup(project.Description), new Markup(project.Version), artifactsTable);
        }

        AnsiConsole.Write(table);

        return 0;
    }
}
