
using System.ComponentModel;
using Shiron.Manila.API;
using Shiron.Manila.Exceptions;
using Spectre.Console;
using Spectre.Console.Cli;
using static Shiron.Manila.CLI.CLIConstants;

namespace Shiron.Manila.CLI.Commands;

[Description("Lists all available artifacts in the current workspace")]
internal sealed class ArtifactsCommand(BaseServiceCotnainer baseServices, Workspace? workspace = null) :
    BaseManilaCommand<ArtifactsCommand.Settings>(baseServices) {

    private readonly Workspace? _workspace = workspace;
    private readonly BaseServiceCotnainer _baseServices = baseServices;

    public sealed class Settings : DefaultCommandSettings { }

    protected override int ExecuteCommand(CommandContext context, Settings settings) {
        if (_workspace == null) {
            _baseServices.Logger.Error(Messages.NoWorkspace);
            return ExitCodes.USER_COMMAND_ERROR;
        }

        AnsiConsole.Write(new Rule("[bold yellow]Available Artifacts[/]").RuleStyle("grey").DoubleBorder());

        var table = new Table().Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("[blue]Artifact[/]"))
            .AddColumn(new TableColumn("[cyan]Project[/]"))
            .AddColumn(new TableColumn("[green]Description[/]"))
            .AddColumn(new TableColumn("[blue]Jobs[/]"));

        foreach (var p in _workspace.Projects) {
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
