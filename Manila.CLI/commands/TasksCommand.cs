using Shiron.Manila.Exceptions;
using Spectre.Console;
using Spectre.Console.Cli;
using static Shiron.Manila.CLI.CLIConstants;

namespace Shiron.Manila.CLI.Commands;

internal sealed class TasksCommand : BaseAsyncManilaCommand<TasksCommand.Settings> {
    public sealed class Settings : DefaultCommandSettings { }

    protected override async System.Threading.Tasks.Task<int> ExecuteCommandAsync(CommandContext context, Settings settings) {
        var engine = ManilaEngine.GetInstance();

        ManilaCLI.InitExtensions();
        await engine.Run();
        if (engine.Workspace == null) throw new ManilaException(Messages.NoWorkspace);

        AnsiConsole.Write(new Rule(string.Format(Format.Rule, Messages.AvailableTasks)).RuleStyle(BorderStyles.Default).DoubleBorder());

        if (engine.Workspace.Tasks.Count > 0) {
            var workspaceTable = new Table().Border(TableBorder.Rounded);
            workspaceTable.AddColumn(new TableColumn(TableColumns.Task));
            workspaceTable.AddColumn(new TableColumn(TableColumns.Description));
            workspaceTable.AddColumn(new TableColumn(TableColumns.Dependencies));

            foreach (var t in engine.Workspace.Tasks) {
                workspaceTable.AddRow(
                    string.Format(Format.TaskIdentifier, t.GetIdentifier()),
                    t.Description ?? "",
                    t.Dependencies.Count > 0 ? string.Format(Format.Dependencies, string.Join(", ", t.Dependencies)) : "");
            }

            AnsiConsole.MarkupLine($"\n{string.Format(Format.Header, Messages.WorkspaceTasks)}");
            AnsiConsole.Write(workspaceTable);
        }

        foreach (var p in engine.Workspace.Projects) {
            var project = p.Value;

            // Only show project table if it has tasks
            if (project.Tasks.Count > 0) {
                var projectTable = new Table().Border(TableBorder.Rounded);
                projectTable.AddColumn(new TableColumn(TableColumns.Task));
                projectTable.AddColumn(new TableColumn(TableColumns.Description));
                projectTable.AddColumn(new TableColumn(TableColumns.Dependencies));

                foreach (var t in project.Tasks) {
                    projectTable.AddRow(
                        string.Format(Format.TaskIdentifier, t.GetIdentifier()),
                        t.Description ?? "",
                        t.Dependencies.Count > 0 ? string.Format(Format.Dependencies, string.Join(", ", t.Dependencies)) : "");
                }

                AnsiConsole.MarkupLine($"\n{string.Format(Format.Header, project.Name)}");
                AnsiConsole.Write(projectTable);
            }

            // Display artifact tasks
            foreach (var artifact in project.Artifacts) {
                if (artifact.Value.Tasks.Length == 0) continue;

                var artifactTable = new Table().Border(TableBorder.Rounded);
                artifactTable.AddColumn(new TableColumn(TableColumns.Task));
                artifactTable.AddColumn(new TableColumn(TableColumns.Description));
                artifactTable.AddColumn(new TableColumn(TableColumns.Dependencies));

                foreach (var t in artifact.Value.Tasks) {
                    artifactTable.AddRow(
                        string.Format(Format.TaskIdentifier, t.GetIdentifier()),
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
