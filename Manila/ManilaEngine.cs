using System.Reflection;
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

public sealed class ManilaEngine(ServiceContainer services, IDirectories directories) {
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
    private readonly ServiceContainer _services = services;

    #endregion

    public IEnumerable<string> DiscoverProjectScripts() {
        var root = _directories.RootDir;
        foreach (var dir in Directory.GetDirectories(root)) {
            foreach (var file in Directory.GetFiles(dir, "Manila.js", SearchOption.AllDirectories)) {
                var path = Path.GetRelativePath(root, file);
                if (Path.GetFileName(path).Equals("Manila.js", StringComparison.OrdinalIgnoreCase)) {
                    yield return path;
                }
            }
        }
    }

    public async Task<Project> RunProjectScript(ScriptContext context, Workspace workspace, WorkspaceScriptBridge workspaceBridge) {
        if (!Path.GetFileName(context.ScriptPath).Equals("manila.js", StringComparison.CurrentCultureIgnoreCase))
            throw new ManilaException($"Project script must be named 'Manila.js', but found '{Path.GetFileName(context.ScriptPath)}'.");

        using (new ProfileScope(_services.Profiler, MethodBase.GetCurrentMethod()!)) {
            var projectRoot = Path.GetDirectoryName(context.ScriptPath) ?? throw new ManilaException($"Could not determine project root from script path: {context.ScriptPath}");
            var projectName = (projectRoot.Split(Path.DirectorySeparatorChar).LastOrDefault() ?? throw new ManilaException($"Could not determine project name from root: {projectRoot}")).ToLower();

            _services.Logger.Log(new ProjectDiscoveredLogEntry(projectRoot, context.ScriptPath));

            var project = new Project(_services.Logger, projectName, projectRoot, _directories.RootDir, workspace);
            var projectBridge = new ProjectScriptBridge(_services.Logger, _services.Profiler, project);

            workspace.Projects.Add(projectName, project);

            context.ApplyEnum<EPlatform>();
            context.ApplyEnum<EArchitecture>();

            context.Init(new(
                _services, context,
                workspaceBridge, workspace,
                projectBridge, project
            ), projectBridge, project);
            try {
                await context.ExecuteAsync(_services.FileHashCache, project);
            } catch {
                throw;
            }

            _services.Logger.Log(new ProjectInitializedLogEntry(project));

            return project;
        }
    }
    /// <summary>
    /// Executes the workspace script (Manila.js in the root directory).
    /// </summary>
    public async Task<Workspace> RunWorkspaceScript(ScriptContext context) {
        using (new ProfileScope(_services.Profiler, MethodBase.GetCurrentMethod()!)) {
            var workspaceRoot = Path.GetDirectoryName(context.ScriptPath) ?? throw new ManilaException($"Could not determine workspace root from script path: {context.ScriptPath}");
            _services.Logger.Debug("Running workspace script: " + context.ScriptPath);

            var workspace = new Workspace(_services.Logger, workspaceRoot);
            var workspaceBridge = new WorkspaceScriptBridge(_services.Logger, _services.Profiler, workspace);

            context.ApplyEnum<EArchitecture>();
            context.ApplyEnum<EPlatform>();

            context.Init(
                new(
                    _services, context,
                    workspaceBridge, workspace,
                    null, null
                ), workspaceBridge, workspace
            );
            try {
                await context.ExecuteAsync(
                    _services.FileHashCache, workspace
                );

                return workspace;
            } catch (Exception e) {
                var ex = new ManilaException($"Failed to execute workspace script: {context.ScriptPath}", e);
                throw ex;
            }
        }
    }

    /// <summary>
    /// Constructs the execution graph and runs the build logic for a specified job.
    /// </summary>
    /// <param name="jobID">The ID of the job to execute.</param>
    public ExecutionGraph CreateExecutionGraph(Workspace workspace) {
        var graph = new ExecutionGraph(_services.Logger, _services.Profiler);

        // Add all existing jobs to the graph, hopefully I'll find a better solution for this in the future
        ExecutionGraph.ExecutionLayer[] layers = [];
        using (new ProfileScope(_services.Profiler, "Building Dependency Tree")) {
            List<Job> Jobs = [.. workspace.Jobs];
            foreach (var p in workspace.Projects.Values) {
                Jobs.AddRange([.. p.Jobs]);
                foreach (var a in p.Artifacts.Values) {
                    Jobs.AddRange([.. a.Jobs]);
                }
            }

            foreach (var t in Jobs) {
                List<ExecutableObject> dependencies = [];
                foreach (var d in t.Dependencies) {
                    dependencies.Add(_services.JobRegistry.GetJob(d) ?? throw new ManilaException($"Dependency '{d}' not found for job '{t.Name}'"));
                }
                graph.Attach(t, dependencies);
            }
        }

        _services.Logger.Log(new BuildLayersLogEntry(layers));

        return graph;
    }

    public void ExecuteBuildLogic(ExecutionGraph graph, string jobID) {
        using (new ProfileScope(_services.Profiler, "Executing Execution Layers")) {
            long startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _services.Logger.Log(new BuildStartedLogEntry());
            var layers = graph.GetExecutionLayers(jobID);

            try {
                int layerIndex = 0;
                foreach (var layer in layers) {
                    using (new ProfileScope(_services.Profiler, $"Executing Layer {layerIndex}")) {
                        Guid layerContextID = Guid.NewGuid();
                        _services.Logger.Log(new BuildLayerStartedLogEntry(layer, layerContextID, layerIndex));
                        using (_services.Logger.LogContext.PushContext(layerContextID)) {
                            List<Task> layerJobs = [];

                            foreach (var o in layer.Items) {
                                layerJobs.Add(Task.Run(() => o.Execute()));
                            }

                            Task.WhenAll(layerJobs).GetAwaiter().GetResult();
                            _services.Logger.Log(new BuildLayerCompletedLogEntry(layer, layerContextID, layerIndex));
                        }

                        layerIndex++;
                    }
                }
                _services.Logger.Log(new BuildCompletedLogEntry(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startTime));
            } catch (Exception e) {
                var ex = new BuildException(e.Message, e);
                _services.Logger.Log(new BuildFailedLogEntry(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startTime, e));
                throw ex;
            }
        }
    }

    public void Dispose() {
        _services.ArtifactManager.FlushCacheToDisk();
    }
}
