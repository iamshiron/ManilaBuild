using Shiron.Manila.Exceptions;
using Spectre.Console;
using Spectre.Console.Cli;
using static Shiron.Manila.CLI.CLIConstants;

namespace Shiron.Manila.CLI.Commands;

internal sealed class JobsCommand : BaseAsyncManilaCommand<JobsCommand.Settings> {
    public sealed class Settings : DefaultCommandSettings { }

    protected override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings) {
        var engine = ManilaEngine.GetInstance();

        ManilaCLI.InitExtensions();
        await engine.Run();
        if (engine.Workspace == null) throw new ManilaException(Messages.NoWorkspace);

        AnsiConsole.Write(new Rule(string.Format(Format.Rule, Messages.AvailableJobs)).RuleStyle(BorderStyles.Default).DoubleBorder());

        if (engine.Workspace.Jobs.Count > 0) {
            var workspaceTable = new Table().Border(TableBorder.Rounded);
            workspaceTable.AddColumn(new TableColumn(TableColumns.Job));
            workspaceTable.AddColumn(new TableColumn(TableColumns.Description));
            workspaceTable.AddColumn(new TableColumn(TableColumns.Dependencies));

            foreach (var t in engine.Workspace.Jobs) {
                workspaceTable.AddRow(
                    string.Format(Format.JobIdentifier, t.GetIdentifier()),
                    t.Description ?? "",
                    t.Dependencies.Count > 0 ? string.Format(Format.Dependencies, string.Join(", ", t.Dependencies)) : "");
            }

            AnsiConsole.MarkupLine($"\n{string.Format(Format.Header, Messages.WorkspaceJobs)}");
            AnsiConsole.Write(workspaceTable);
        }

        foreach (var p in engine.Workspace.Projects) {
            var project = p.Value;

            // Only show project table if it has jobs
            if (project.Jobs.Count > 0) {
                var projectTable = new Table().Border(TableBorder.Rounded);
                projectTable.AddColumn(new TableColumn(TableColumns.Job));
                projectTable.AddColumn(new TableColumn(TableColumns.Description));
                projectTable.AddColumn(new TableColumn(TableColumns.Dependencies));

                foreach (var t in project.Jobs) {
                    projectTable.AddRow(
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

                var artifactTable = new Table().Border(TableBorder.Rounded);
                artifactTable.AddColumn(new TableColumn(TableColumns.Job));
                artifactTable.AddColumn(new TableColumn(TableColumns.Description));
                artifactTable.AddColumn(new TableColumn(TableColumns.Dependencies));

                foreach (var t in artifact.Value.Jobs) {
                    artifactTable.AddRow(
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
