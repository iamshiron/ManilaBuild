using System.ComponentModel;
using Shiron.Manila.Exceptions;
using Spectre.Console;
using Spectre.Console.Cli;
using static Shiron.Manila.CLI.CLIConstants;

namespace Shiron.Manila.CLI.Commands;

[Description("Lists all available projects in the current workspace")]
internal sealed class ProjectsCommand : BaseAsyncManilaCommand<ProjectsCommand.Settings> {
    public sealed class Settings : DefaultCommandSettings { }

    protected override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings) {
        if (ManilaCLI.Profiler == null || ManilaCLI.ManilaEngine == null || ManilaCLI.Logger == null)
            throw new ManilaException("Manila engine, profiler, or logger is not initialized.");

        var engine = ManilaCLI.ManilaEngine;

        await ManilaCLI.InitExtensions(ManilaCLI.Profiler, engine);
        await engine.Run();
        if (engine.Workspace == null) throw new ManilaException(Messages.NoWorkspace);

        AnsiConsole.Write(new Rule(string.Format(Format.Rule, Messages.AvailableProjects)).RuleStyle(BorderStyles.Default).DoubleBorder());

        var table = new Table().Border(TableBorder.Rounded)
            .AddColumn(new TableColumn(TableColumns.Project))
            .AddColumn(new TableColumn(TableColumns.Description))
            .AddColumn(new TableColumn(TableColumns.Version))
            .AddColumn(new TableColumn(TableColumns.Artifacts));

        foreach (var p in engine.Workspace.Projects) {
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
