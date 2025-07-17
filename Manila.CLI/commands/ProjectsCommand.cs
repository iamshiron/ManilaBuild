using Shiron.Manila.Exceptions;
using Spectre.Console;
using Spectre.Console.Cli;
using static Shiron.Manila.CLI.CLIConstants;

namespace Shiron.Manila.CLI.Commands;

internal sealed class ProjectsCommand : BaseAsyncManilaCommand<ProjectsCommand.Settings> {
    public sealed class Settings : DefaultCommandSettings { }

    protected override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings) {
        var engine = ManilaEngine.GetInstance();

        await ManilaCLI.InitExtensions();
        await engine.Run();
        if (engine.Workspace == null) throw new ManilaException(Messages.NoWorkspace);

        AnsiConsole.Write(new Rule(string.Format(Format.Rule, Messages.AvailableProjects)).RuleStyle(BorderStyles.Default).DoubleBorder());

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn(new TableColumn(TableColumns.Project));
        table.AddColumn(new TableColumn(TableColumns.Description));
        table.AddColumn(new TableColumn(TableColumns.Version));
        table.AddColumn(new TableColumn(TableColumns.Artifacts));

        foreach (var p in engine.Workspace.Projects) {
            var project = p.Value;
            var artifactsTable = new Table().Border(TableBorder.Rounded);
            artifactsTable.AddColumn(new TableColumn(TableColumns.Artifact));
            artifactsTable.AddColumn(new TableColumn(TableColumns.Description));

            foreach (var (name, artifact) in project.Artifacts) {
                artifactsTable.AddRow(string.Format(Format.JobIdentifier, name), artifact.Description);
            }

            table.AddRow(new Markup($"[cyan bold]{project.Name}[/]"), new Markup(project.Description ?? "[grey](none)[/]"), new Markup(project.Version ?? "[grey](none)[/]"), artifactsTable);
        }

        AnsiConsole.Write(table);

        return ExitCodes.SUCCESS;
    }
}
