
using System.ComponentModel;
using Newtonsoft.Json;
using Shiron.Manila.API;
using Spectre.Console.Cli;
using static Shiron.Manila.CLI.CLIConstants;

namespace Shiron.Manila.CLI.Commands.API;

[Description("Retreive jobs information")]
internal sealed class APIJobsCommand(BaseServiceCotnainer baseServices, Workspace? workspace = null) : BaseManilaCommand<APIJobsCommand.Settings> {
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
        var jobs = new List<object>();

        // Workspace jobs
        foreach (var job in workspace.Jobs) {
            if (settings.Project != null) continue; // Skip workspace jobs if filtering by project

            var jobData = new {
                name = job.Name,
                identifier = job.GetIdentifier(),
                description = job.Description,
                dependencies = job.Dependencies,
                type = ProjectTypes.Workspace,
                project = (string?) null,
                artifact = (string?) null,
                blocking = job.Blocking
            };

            if (settings.Detailed) {
                jobs.Add(new {
                    jobData.name,
                    jobData.identifier,
                    jobData.description,
                    jobData.dependencies,
                    jobData.type,
                    jobData.project,
                    jobData.artifact,
                    jobData.blocking,
                    executionOrder = job.GetExecutionOrder(),
                    jobId = job.JobID,
                    component = job.Component?.GetIdentifier()
                });
            } else {
                jobs.Add(jobData);
            }
        }

        // Project jobs
        foreach (var (projectName, project) in workspace.Projects) {
            if (settings.Project != null && settings.Project != projectName) continue;

            // Project-level jobs
            foreach (var job in project.Jobs) {
                var jobData = new {
                    name = job.Name,
                    identifier = job.GetIdentifier(),
                    description = job.Description,
                    dependencies = job.Dependencies,
                    type = ProjectTypes.Project,
                    project = projectName,
                    artifact = (string?) null,
                    blocking = job.Blocking
                };

                if (settings.Detailed) {
                    jobs.Add(new {
                        jobData.name,
                        jobData.identifier,
                        jobData.description,
                        jobData.dependencies,
                        jobData.type,
                        jobData.project,
                        jobData.artifact,
                        jobData.blocking,
                        executionOrder = job.GetExecutionOrder(),
                        jobId = job.JobID,
                        component = job.Component?.GetIdentifier()
                    });
                } else {
                    jobs.Add(jobData);
                }
            }

            // Artifact jobs
            foreach (var (artifactName, artifact) in project.Artifacts) {
                foreach (var job in artifact.Jobs) {
                    var jobData = new {
                        name = job.Name,
                        identifier = job.GetIdentifier(),
                        description = job.Description,
                        dependencies = job.Dependencies,
                        type = ProjectTypes.Artifact,
                        project = projectName,
                        artifact = artifactName,
                        blocking = job.Blocking
                    };

                    if (settings.Detailed) {
                        jobs.Add(new {
                            jobData.name,
                            jobData.identifier,
                            jobData.description,
                            jobData.dependencies,
                            jobData.type,
                            jobData.project,
                            jobData.artifact,
                            jobData.blocking,
                            executionOrder = job.GetExecutionOrder(),
                            jobId = job.JobID,
                            component = job.Component?.GetIdentifier()
                        });
                    } else {
                        jobs.Add(jobData);
                    }
                }
            }
        }

        return new {
            jobs = jobs.ToArray(),
            count = jobs.Count
        };
    }
}
