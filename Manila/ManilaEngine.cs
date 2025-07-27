using System.Reflection;
using System.Threading.Tasks;
using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;
using Shiron.Manila.API;
using Shiron.Manila.API.Bridges;
using Shiron.Manila.Artifacts;
using Shiron.Manila.Caching;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Ext;
using Shiron.Manila.Logging;
using Shiron.Manila.Profiling;
using Shiron.Manila.Registries;
using Shiron.Manila.Utils;

namespace Shiron.Manila;

public sealed class ManilaEngine(BaseServiceContainer baseServices, IDirectories directories) {
    #region Properties

    /// <summary>
    /// Gets the timestamp (in Unix milliseconds) when the engine was created.
    /// </summary>
    public readonly long EngineCreatedTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// <summary>
    /// Gets the version of the Manila engine.
    /// </summary>
    public static readonly string VERSION = "0.0.1";

    private readonly IDirectories _directories = directories;
    private readonly BaseServiceContainer _baseServices = baseServices;

    #endregion

    public IEnumerable<string> DiscoverProjectScripts(IProfiler profiler) {
        using (new ProfileScope(profiler, "Discovering Project Scripts")) {
            List<string> paths = [];
            var root = _directories.RootDir;
            try {
                foreach (var dir in Directory.GetDirectories(root)) {
                    foreach (var file in Directory.GetFiles(dir, "Manila.js", SearchOption.AllDirectories)) {
                        var path = Path.GetRelativePath(root, file);
                        if (Path.GetFileName(path).Equals("Manila.js", StringComparison.OrdinalIgnoreCase)) {
                            paths.Add(path);
                        }
                    }
                }
            } catch (UnauthorizedAccessException uae) {
                throw new EnvironmentException($"Permission denied while searching for project scripts in '{root}'. Please check directory permissions.", uae);
            } catch (DirectoryNotFoundException dnfe) {
                throw new EnvironmentException($"Could not find a directory while searching for project scripts. The path may be invalid: '{root}'.", dnfe);
            } catch (Exception ex) {
                throw new EnvironmentException($"An unexpected error occurred while discovering project scripts in '{root}'.", ex);
            }
            return paths;
        }
    }

    public async Task<Project> RunProjectScriptAsync(ServiceContainer services, ScriptContext context, Workspace workspace, WorkspaceScriptBridge workspaceBridge) {
        if (!Path.GetFileName(context.ScriptPath).Equals("manila.js", StringComparison.CurrentCultureIgnoreCase))
            throw new ConfigurationException($"Project script must be named 'Manila.js', but found '{Path.GetFileName(context.ScriptPath)}' at '{context.ScriptPath}'.");

        using (new ProfileScope(_baseServices.Profiler, $"Running Project Script '{context.ScriptPath}'")) {
            var projectRoot = Path.GetDirectoryName(context.ScriptPath);
            if (string.IsNullOrEmpty(projectRoot)) {
                throw new ConfigurationException($"Could not determine project root directory from script path: {context.ScriptPath}");
            }

            var projectName = Path.GetFileName(projectRoot);
            if (string.IsNullOrEmpty(projectName)) {
                throw new ConfigurationException($"Could not determine project name from its root directory: {projectRoot}");
            }
            projectName = projectName.ToLower();

            _baseServices.Logger.Log(new ProjectDiscoveredLogEntry(projectRoot, context.ScriptPath));

            var project = new Project(_baseServices.Logger, projectName, projectRoot, _directories.RootDir, workspace);
            var projectBridge = new ProjectScriptBridge(project);

            workspace.Projects.Add(projectName, project);

            context.Init(new(_baseServices, services, context, workspaceBridge, workspace, projectBridge, project), projectBridge, project);
            try {
                await context.ExecuteAsync(services.FileHashCache, project);
            } catch (ScriptEngineException se) {
                throw new ScriptExecutionException(
                    message: $"An error occurred while executing project script: '{context.ScriptPath}'. Details: {se.Message}",
                    scriptPath: context.ScriptPath,
                    jsErrorName: se.ScriptException.GetProperty("name")?.ToString(),
                    jsStackTrace: se.ScriptException.GetProperty("stack")?.ToString(),
                    innerException: se
                );
            } catch (Exception e) {
                throw new BuildProcessException($"An unexpected error occurred while processing project script: {context.ScriptPath}", e);
            }

            _baseServices.Logger.Log(new ProjectInitializedLogEntry(project));

            return project;
        }
    }

    public async Task<Workspace> RunWorkspaceScriptAsync(ServiceContainer services, ScriptContext context) {
        using (new ProfileScope(_baseServices.Profiler, "Running Workspace Script")) {
            var workspaceRoot = Path.GetDirectoryName(context.ScriptPath);
            if (string.IsNullOrEmpty(workspaceRoot)) {
                throw new ConfigurationException($"Could not determine workspace root from script path: {context.ScriptPath}");
            }
            _baseServices.Logger.Debug("Running workspace script: " + context.ScriptPath);

            var workspace = new Workspace(_baseServices.Logger, workspaceRoot);
            var workspaceBridge = new WorkspaceScriptBridge(_baseServices.Logger, _baseServices.Profiler, workspace);

            context.Init(new(_baseServices, services, context, workspaceBridge, workspace, null, null), workspaceBridge, workspace);
            try {
                await context.ExecuteAsync(services.FileHashCache, workspace);

                return workspace;
            } catch (ScriptEngineException se) {
                throw new ScriptExecutionException(
                    message: $"An error occurred while executing workspace script: '{context.ScriptPath}'. Details: {se.Message}",
                    scriptPath: context.ScriptPath,
                    jsErrorName: se.ScriptException.GetProperty("name")?.ToString(),
                    jsStackTrace: se.ScriptException.GetProperty("stack")?.ToString(),
                    innerException: se
                );
            } catch (Exception e) {
                throw new ConfigurationException($"An unexpected error occurred while processing workspace script: {context.ScriptPath}", e);
            }
        }
    }

    public ExecutionGraph CreateExecutionGraph(ServiceContainer services, BaseServiceContainer baseServices, Workspace workspace) {
        var graph = new ExecutionGraph(_baseServices.Logger, _baseServices.Profiler);

        using (new ProfileScope(_baseServices.Profiler, "Building Dependency Tree")) {
            baseServices.Logger.Debug($"Building dependency tree for workspace. Project: {workspace.Projects.Count}");

            List<Job> Jobs = [.. workspace.Jobs];

            foreach (var p in workspace.Projects.Values) {
                Jobs.AddRange([.. p.Jobs]);
                foreach (var a in p.Artifacts.Values) {
                    Jobs.AddRange([.. a.Jobs]);
                }
            }

            baseServices.Logger.Debug($"Found {Jobs.Count} base jobs in workspace.");

            foreach (var t in Jobs) {
                List<ExecutableObject> dependencies = [];
                foreach (var d in t.Dependencies) {
                    var dependencyJob = services.JobRegistry.GetJob(d)
                        ?? throw new DependencyNotFoundException(
                            message: $"Dependency '{d}' not found for job '{t.Name}'. Ensure the dependency is defined and spelled correctly.",
                            jobName: t.Name,
                            missingDependencyName: d
                        );
                    dependencies.Add(dependencyJob);
                }
                graph.Attach(t, dependencies);
            }
        }
        return graph;
    }

    public async Task ExecuteBuildLogicAsync(ExecutionGraph graph, string jobID) {
        using (new ProfileScope(_baseServices.Profiler, "Executing Execution Layers")) {
            long startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _baseServices.Logger.Log(new BuildStartedLogEntry());

            ExecutionGraph.ExecutionLayer[] layers;
            try {
                layers = graph.GetExecutionLayers(jobID);
            } catch (KeyNotFoundException knfe) {
                throw new ConfigurationException($"The specified entry point job '{jobID}' was not found in the execution graph.", knfe);
            } catch (InvalidOperationException ioe) when (ioe.Message.Contains("cycle", StringComparison.OrdinalIgnoreCase)) {
                throw new InternalLogicException("A dependency cycle was detected in the job graph. Cannot determine execution order.", ioe);
            }

            _baseServices.Logger.Log(new BuildLayersLogEntry(layers));

            List<Task> backgroundJobs = [];

            try {
                int layerIndex = 0;
                foreach (var layer in layers) {
                    using (new ProfileScope(_baseServices.Profiler, $"Executing Layer {layerIndex}")) {
                        Guid layerContextID = Guid.NewGuid();
                        _baseServices.Logger.Log(new BuildLayerStartedLogEntry(layer, layerContextID, layerIndex));
                        using (_baseServices.Logger.LogContext.PushContext(layerContextID)) {
                            List<Task> layerJobs = [];

                            foreach (var o in layer.Items) {
                                _baseServices.Logger.Debug($"Blocking: {o.IsBlocking()}");

                                if (o.IsBlocking()) layerJobs.Add(Task.Run(o.RunAsync));
                                else backgroundJobs.Add(Task.Run(o.RunAsync));
                            }

                            _baseServices.Logger.Debug($"Waiting for {layerJobs.Count} blocking jobs in layer {layerIndex} to complete.");
                            await Task.WhenAll(layerJobs);
                            _baseServices.Logger.Log(new BuildLayerCompletedLogEntry(layer, layerContextID, layerIndex));
                        }
                        layerIndex++;
                    }
                }

                await Task.WhenAll(backgroundJobs);
                _baseServices.Logger.Log(new BuildCompletedLogEntry(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startTime));
            } catch (Exception e) {
                var finalException = e;
                if (e is AggregateException ae && ae.InnerExceptions.Count > 0) {
                    finalException = ae.InnerExceptions[0];
                }

                _baseServices.Logger.Log(new BuildFailedLogEntry(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startTime, finalException));

                throw new BuildProcessException($"The build failed during job execution. See inner exception for details from the failed job.", finalException);
            }
        }
    }
}
