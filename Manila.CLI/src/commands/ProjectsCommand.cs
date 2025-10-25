using System.ComponentModel;
using Shiron.Manila.API;
using Shiron.Manila.API.Exceptions;
using Spectre.Console;
using Spectre.Console.Cli;
using static Shiron.Manila.CLI.CLIConstants;

namespace Shiron.Manila.CLI.Commands;

[Description("Lists all available projects in the current workspace")]
internal sealed class ProjectsCommand(BaseServiceContainer baseServices, Workspace? workspace = null) :
    BaseManilaCommand<ProjectsCommand.Settings>(baseServices) {

    private readonly Workspace? _workspace = workspace;
    private readonly BaseServiceContainer _baseServices = baseServices;

    public sealed class Settings : DefaultCommandSettings { }

    protected override int ExecuteCommand(CommandContext context, Settings settings) {
        if (_workspace == null) {
            _baseServices.Logger.Error(Messages.NoWorkspace);
            return ExitCodes.USER_COMMAND_ERROR;
        }

        AnsiConsole.Write(new Rule(string.Format(Format.Rule, Messages.AvailableProjects)).RuleStyle(BorderStyles.Default).DoubleBorder());

        var table = new Table().Border(TableBorder.Rounded)
            .AddColumn(new TableColumn(TableColumns.Project))
            .AddColumn(new TableColumn(TableColumns.Description))
            .AddColumn(new TableColumn(TableColumns.Version))
            .AddColumn(new TableColumn(TableColumns.Artifacts));

        foreach (var p in _workspace.Projects) {
            var project = p.Value;
            var artifactsTable = new Table().Border(TableBorder.Rounded)
                .AddColumn(new TableColumn(TableColumns.Artifact))
                .AddColumn(new TableColumn(TableColumns.Description));

            foreach (var (name, artifact) in project.Artifacts) {
                _ = artifactsTable.AddRow(string.Format(Format.JobIdentifier, name), artifact.Description);
            }

            _ = table.AddRow(new Markup($"[cyan bold]{project.Name}[/]"), new Markup(project.Description ?? "[grey](none)[/]"), new Markup(project.Version ?? "[grey](none)[/]"), artifactsTable);
        }

        AnsiConsole.Write(table);

        return ExitCodes.SUCCESS;
    }
}
