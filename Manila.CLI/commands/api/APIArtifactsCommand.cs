
using System.ComponentModel;
using Newtonsoft.Json;
using Shiron.Manila.API;
using Spectre.Console.Cli;
using static Shiron.Manila.CLI.CLIConstants;

namespace Shiron.Manila.CLI.Commands.API;

[Description("Retrieve artifacts information")]
internal sealed class APIArtifactsCommand(BaseServiceCotnainer baseServices, Workspace? workspace = null) :
    BaseManilaCommand<APIArtifactsCommand.Settings>(baseServices) {

    private readonly BaseServiceCotnainer _baseServices = baseServices;
    private readonly Workspace? _workspace = workspace;

    public class Settings : APICommandSettings {
        [Description("Name of the project to filter by")]
        [CommandOption("--project|-p <NAME>")]
        public string? Project { get; set; } = null;

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
        var artifacts = new List<object>();

        foreach (var (projectName, project) in workspace.Projects) {
            if (settings.Project != null && settings.Project != projectName) continue;

            foreach (var (artifactName, artifact) in project.Artifacts) {
                var artifactData = new {
                    name = artifactName,
                    description = artifact.Description,
                    project = projectName,
                    jobCount = artifact.Jobs.Length,
                    component = artifact.PluginComponent.Format()
                };

                if (settings.Detailed) {
                    artifacts.Add(new {
                        artifactData.name,
                        artifactData.description,
                        artifactData.project,
                        artifactData.jobCount,
                        component = artifact.PluginComponent.Format(),
                        jobs = artifact.Jobs.Select(t => new {
                            name = t.Name,
                            identifier = t.GetIdentifier(),
                            description = t.Description,
                            dependencies = t.Dependencies,
                            blocking = t.Blocking,
                            component = t.Component?.GetIdentifier()
                        }).ToArray()
                    });
                } else {
                    artifacts.Add(artifactData);
                }
            }
        }

        return new {
            artifacts = artifacts.ToArray(),
            count = artifacts.Count
        };
    }
}
