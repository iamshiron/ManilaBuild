using System.ComponentModel;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Shiron.Manila.API;
using Shiron.Manila.Artifacts;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Ext;
using Spectre.Console.Cli;
using static Shiron.Manila.CLI.CLIConstants;

namespace Shiron.Manila.CLI.Commands;

[Description("API commands for retrieving information as JSON output")]
internal sealed class ApiCommand(BaseServiceCotnainer baseServices, ManilaEngine? engine = null, ServiceContainer? services = null, Workspace? workspace = null) : BaseManilaCommand<ApiCommand.Settings> {
    private readonly ServiceContainer? _services = services;
    private readonly ManilaEngine? _engine = engine;
    private readonly Workspace? _workspace = workspace;
    private readonly BaseServiceCotnainer _baseServices = baseServices;

    public sealed class Settings : DefaultCommandSettings {
        [Description("API subcommand: jobs, artifacts, projects, workspace, plugins")]
        [CommandArgument(0, "[subcommand]")]
        public string Subcommand { get; set; } = string.Empty;

        [Description("Filter results by project name")]
        [CommandOption("--project")]
        public string? Project { get; set; }

        [Description("Include detailed information")]
        [CommandOption("--detailed")] // Can't use constant in attribute
        [DefaultValue(false)]
        public bool Detailed { get; set; }
    }

    private static readonly JsonSerializerSettings _jsonSettings = new() {
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
        Formatting = Formatting.Indented,
        Converters = { new StringEnumConverter() },
        NullValueHandling = NullValueHandling.Ignore,
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore
    };

    protected override int ExecuteCommand(CommandContext context, Settings settings) {
        if (_workspace == null || _services == null || _engine == null) {
            _baseServices.Logger.Error(Messages.ManilaEngineNotInitialized);
            return ExitCodes.USER_COMMAND_ERROR;
        }

        var cmd = settings.Subcommand?.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(cmd) || !ApiSubcommands.All.Contains(cmd)) {
            _baseServices.Logger.Error(Messages.InvalidSubCommand.ApiSubcommand);
            return ExitCodes.USER_COMMAND_ERROR;
        }

        var result = cmd switch {
            "jobs" => GetJobsData(_workspace, settings),
            "artifacts" => GetArtifactsData(_workspace, settings),
            "projects" => GetProjectsData(_workspace, settings),
            "workspace" => GetWorkspaceData(_workspace, settings),
            "plugins" => GetPluginsData(_services.ExtensionManager, settings),
            _ => throw new ManilaException($"Unknown API subcommand: {settings.Subcommand}. Available: {string.Join(", ", ApiSubcommands.All)}")
        };

        var json = JsonConvert.SerializeObject(result, _jsonSettings);
        Console.WriteLine(json);

        return ExitCodes.SUCCESS;
    }

    private static object GetJobsData(Workspace workspace, Settings settings) {
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

    private static object GetArtifactsData(Workspace workspace, Settings settings) {
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

    private static object GetProjectsData(Workspace workspace, Settings settings) {
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

    private static object GetPluginsData(IExtensionManager mgr, Settings settings) {
        var list = new List<object>();
        foreach (var plugin in mgr.Plugins) {
            var pluginData = new {
                group = plugin.Group,
                name = plugin.Name,
                version = plugin.Version
            };
            if (settings.Detailed) {
                list.Add(new {
                    pluginData.group,
                    pluginData.name,
                    pluginData.version,
                    components = plugin.Components.Keys.ToArray(),
                    apiClasses = plugin.APIClasses.Keys.ToArray()
                });
            } else {
                list.Add(pluginData);
            }
        }
        return new { plugins = list.ToArray(), count = list.Count };
    }

    private static object GetWorkspaceData(Workspace workspace, Settings settings) {
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
