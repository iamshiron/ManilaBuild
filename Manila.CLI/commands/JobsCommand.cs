using System.ComponentModel;
using Shiron.Manila.API;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Logging;
using Spectre.Console;
using Spectre.Console.Cli;
using static Shiron.Manila.CLI.CLIConstants;

namespace Shiron.Manila.CLI.Commands;

[Description("Lists all available jobs in the current workspace")]
internal sealed class JobsCommand(BaseServiceContainer baseServices, Workspace? workspace = null) :
    BaseManilaCommand<JobsCommand.Settings>(baseServices) {

    private readonly Workspace? _workspace = workspace;
    private readonly BaseServiceContainer _baseServices = baseServices;

    public sealed class Settings : DefaultCommandSettings { }

    protected override int ExecuteCommand(CommandContext context, Settings settings) {
        if (_workspace == null) {
            _baseServices.Logger.Error(Messages.NoWorkspace);
            return ExitCodes.USER_COMMAND_ERROR;
        }

        AnsiConsole.Write(new Rule(string.Format(Format.Rule, Messages.AvailableJobs)).RuleStyle(BorderStyles.Default).DoubleBorder());

        if (_workspace.Jobs.Count > 0) {
            var workspaceTable = new Table().Border(TableBorder.Rounded)
                .AddColumn(new TableColumn(TableColumns.Job))
                .AddColumn(new TableColumn(TableColumns.Description))
                .AddColumn(new TableColumn(TableColumns.Dependencies));

            foreach (var t in _workspace.Jobs) {
                _ = workspaceTable.AddRow(
                    string.Format(Format.JobIdentifier, t.GetIdentifier()),
                    t.Description ?? "",
                    t.Dependencies.Count > 0 ? string.Format(Format.Dependencies, string.Join(", ", t.Dependencies)) : "");
            }

            AnsiConsole.MarkupLine($"\n{string.Format(Format.Header, Messages.WorkspaceJobs)}");
            AnsiConsole.Write(workspaceTable);
        }

        foreach (var p in _workspace.Projects) {
            var project = p.Value;

            // Only show project table if it has jobs
            if (project.Jobs.Count > 0) {
                var projectTable = new Table().Border(TableBorder.Rounded)
                    .AddColumn(new TableColumn(TableColumns.Job))
                    .AddColumn(new TableColumn(TableColumns.Description))
                    .AddColumn(new TableColumn(TableColumns.Dependencies));

                foreach (var t in project.Jobs) {
                    _ = projectTable.AddRow(
                        string.Format(Format.JobIdentifier, t.GetIdentifier()),
                        t.Description ?? "",
                        t.Dependencies.Count > 0 ? string.Format(Format.Dependencies, string.Join(", ", t.Dependencies)) : "");
                }

                AnsiConsole.MarkupLine($"\n{string.Format(Format.Header, project.Name)}");
                AnsiConsole.Write(projectTable);
            }

            // Display artifact jobs
            foreach (var artifact in project.Artifacts) {
                if (artifact.Value.Jobs.Length == 0) continue;

                var artifactTable = new Table().Border(TableBorder.Rounded)
                    .AddColumn(new TableColumn(TableColumns.Job))
                    .AddColumn(new TableColumn(TableColumns.Description))
                    .AddColumn(new TableColumn(TableColumns.Dependencies));

                foreach (var t in artifact.Value.Jobs) {
                    _ = artifactTable.AddRow(
                        string.Format(Format.JobIdentifier, t.GetIdentifier()),
                        t.Description ?? "",
                        t.Dependencies.Count > 0 ? string.Format(Format.Dependencies, string.Join(", ", t.Dependencies)) : "");
                }

                AnsiConsole.MarkupLine($"\n{string.Format(Format.SubHeader, $"{project.Name} → {artifact.Key}")}");
                AnsiConsole.Write(artifactTable);
            }
        }

        return ExitCodes.SUCCESS;
    }
}
