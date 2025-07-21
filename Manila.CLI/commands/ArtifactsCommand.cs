
using System.ComponentModel;
using Shiron.Manila.Exceptions;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shiron.Manila.CLI.Commands;

[Description("Lists all available artifacts in the current workspace")]
internal sealed class ArtifactsCommand : AsyncCommand<ArtifactsCommand.Settings> {
    public sealed class Settings : DefaultCommandSettings { }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings) {
        if (ManilaCLI.Profiler == null || ManilaCLI.ManilaEngine == null || ManilaCLI.Logger == null)
            throw new ManilaException("Manila engine, profiler, or logger is not initialized.");

        var engine = ManilaCLI.ManilaEngine;
        await ManilaCLI.InitExtensions(ManilaCLI.Profiler, engine);
        await engine.Run();
        if (engine.Workspace == null) throw new ManilaException("Not inside a workspace");

        AnsiConsole.Write(new Rule("[bold yellow]Available Artifacts[/]").RuleStyle("grey").DoubleBorder());

        var table = new Table().Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("[blue]Artifact[/]"))
            .AddColumn(new TableColumn("[cyan]Project[/]"))
            .AddColumn(new TableColumn("[green]Description[/]"))
            .AddColumn(new TableColumn("[blue]Jobs[/]"));

        foreach (var p in engine.Workspace.Projects) {
            var project = p.Value;

            foreach (var (name, artifact) in project.Artifacts) {
                var artifactsTable = new Table()
                    .AddColumn(new TableColumn("[cyan]Job[/]"))
                    .AddColumn(new TableColumn("[green]Description[/]"));

                foreach (var job in artifact.Jobs) {
                    _ = artifactsTable.AddRow($"[cyan bold]{job.Name}[/]", job.Description);
                }

                _ = table.AddRow(
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
