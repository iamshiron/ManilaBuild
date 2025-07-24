
using System.ComponentModel;
using Newtonsoft.Json;
using Shiron.Manila.API;
using Spectre.Console.Cli;
using static Shiron.Manila.CLI.CLIConstants;

namespace Shiron.Manila.CLI.Commands.API;

[Description("Retrieve workspace information")]
internal sealed class APIWorkspaceCommand(BaseServiceCotnainer baseServices, Workspace? workspace = null) : BaseManilaCommand<APIWorkspaceCommand.Settings> {
    private readonly BaseServiceCotnainer _baseServices = baseServices;
    private readonly Workspace? _workspace = workspace;

    public class Settings : APICommandSettings {
        [Description("Name of the project to filter by")]
        [CommandOption("--project|-p <NAME>")]
        public string? Filter { get; set; } = null;

        [Description("Include detailed information")]
        [CommandOption("--detailed")]
        public bool Detailed { get; set; } = false;

        [Description("Output in compact format")]
        [CommandOption("--no-indent")]
        public bool NoIndent { get; set; } = false;

        [Description("No null values in output")]
        [CommandOption("--no-null-values")]
        public bool NoNullValues { get; set; } = false;

        [Description("Include default values in output")]
        [CommandOption("--include-default-values")]
        public bool IncludeDefaultValues { get; set; } = false;
    }

    protected override int ExecuteCommand(CommandContext context, Settings settings) {
        if (_workspace == null) {
            _baseServices.Logger.Error(Messages.NoWorkspace);
            return ExitCodes.USER_COMMAND_ERROR;
        }

        Console.WriteLine(APICommandHelpers.FormatData(
            GetData(_workspace, settings),
            settings.NoIndent, settings.NoNullValues, settings.IncludeDefaultValues
        ));

        return ExitCodes.SUCCESS;
    }

    private static object GetData(Workspace workspace, Settings settings) {
        var workspaceData = new {
            location = workspace.Path.Handle,
            identifier = workspace.GetIdentifier(),
            projectCount = workspace.Projects.Count,
            jobCount = workspace.Jobs.Count
        };

        if (settings.Detailed) {
            return new {
                workspaceData.location,
                workspaceData.identifier,
                workspaceData.projectCount,
                workspaceData.jobCount,
                projects = workspace.Projects.Select(p => new {
                    name = p.Key,
                    location = p.Value.Path.Handle,
                    jobCount = p.Value.Jobs.Count,
                    artifactCount = p.Value.Artifacts.Count
                }).ToArray(),
                jobs = workspace.Jobs.Select(t => new {
                    name = t.Name,
                    identifier = t.GetIdentifier(),
                    description = t.Description
                }).ToArray()
            };
        }

        return workspaceData;
    }
}
