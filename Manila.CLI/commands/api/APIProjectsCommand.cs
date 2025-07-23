
using System.ComponentModel;
using Newtonsoft.Json;
using Shiron.Manila.API;
using Spectre.Console.Cli;
using static Shiron.Manila.CLI.CLIConstants;

namespace Shiron.Manila.CLI.Commands.API;

[Description("Retrieve projects information")]
internal sealed class APIProjectsCommand(BaseServiceCotnainer baseServices, Workspace? workspace = null) : BaseManilaCommand<APIProjectsCommand.Settings> {
    private readonly BaseServiceCotnainer _baseServices = baseServices;
    private readonly Workspace? _workspace = workspace;

    public class Settings : APICommandSettings {
        [Description("Name of the project to filter by")]
        [CommandOption("--project|-p <NAME>")]
        public string? Project { get; set; } = null;
    }

    protected override int ExecuteCommand(CommandContext context, Settings settings) {
        if (_workspace == null) {
            _baseServices.Logger.Error(Messages.NoWorkspace);
            return ExitCodes.USER_COMMAND_ERROR;
        }

        Console.WriteLine(APICommandHelpers.FormatData(
            GetData(_workspace, settings),
            settings
        ));

        return ExitCodes.SUCCESS;
    }

    private static object GetData(Workspace workspace, Settings settings) {
        var projects = new List<object>();

        foreach (var (projectName, project) in workspace.Projects) {
            if (settings.Project != null && settings.Project != projectName) continue;

            var projectData = new {
                name = project.Name,
                identifier = project.GetIdentifier(),
                description = project.Description,
                version = project.Version,
                group = project.Group,
                location = project.Path.Handle,
                jobCount = project.Jobs.Count,
                artifactCount = project.Artifacts.Count,
                sourceSetCount = project.SourceSets.Count
            };

            if (settings.Detailed) {
                projects.Add(new {
                    projectData.name,
                    projectData.identifier,
                    projectData.description,
                    projectData.version,
                    projectData.group,
                    projectData.location,
                    projectData.jobCount,
                    projectData.artifactCount,
                    projectData.sourceSetCount,
                    jobs = project.Jobs.Select(t => new {
                        name = t.Name,
                        identifier = t.GetIdentifier(),
                        description = t.Description
                    }).ToArray(),
                    artifacts = project.Artifacts.Select(a => new {
                        name = a.Key,
                        description = a.Value.Description,
                        jobCount = a.Value.Jobs.Length,
                        component = a.Value.PluginComponent.Format()
                    }).ToArray(),
                    sourceSets = project.SourceSets.Select(s => new {
                        name = s.Key,
                        root = s.Value.Root
                    }).ToArray()
                });
            } else {
                projects.Add(projectData);
            }
        }

        return new {
            projects = projects.ToArray(),
            count = projects.Count
        };
    }
}
