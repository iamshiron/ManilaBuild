using System.Reflection;
using System.Threading.Tasks;
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

public sealed class ManilaEngine(BaseServiceCotnainer baseServices, IDirectories directories) {
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
    private readonly BaseServiceCotnainer _baseServices = baseServices;

    #endregion

    public IEnumerable<string> DiscoverProjectScripts(IProfiler profiler) {
        using (new ProfileScope(profiler, "Discovering Project Scripts")) {
            List<string> paths = [];
            var root = _directories.RootDir;
            foreach (var dir in Directory.GetDirectories(root)) {
                foreach (var file in Directory.GetFiles(dir, "Manila.js", SearchOption.AllDirectories)) {
                    var path = Path.GetRelativePath(root, file);
                    if (Path.GetFileName(path).Equals("Manila.js", StringComparison.OrdinalIgnoreCase)) {
                        paths.Add(path);
                    }
                }
            }
            return paths;
        }
    }

    public async Task<Project> RunProjectScriptAsync(ServiceContainer services, ScriptContext context, Workspace workspace, WorkspaceScriptBridge workspaceBridge) {
        if (!Path.GetFileName(context.ScriptPath).Equals("manila.js", StringComparison.CurrentCultureIgnoreCase))
            throw new ManilaException($"Project script must be named 'Manila.js', but found '{Path.GetFileName(context.ScriptPath)}'.");

        using (new ProfileScope(_baseServices.Profiler, $"Running Project Script '{context.ScriptPath}'")) {
            var projectRoot = Path.GetDirectoryName(context.ScriptPath) ?? throw new ManilaException($"Could not determine project root from script path: {context.ScriptPath}");
            var projectName = (projectRoot.Split(Path.DirectorySeparatorChar).LastOrDefault() ?? throw new ManilaException($"Could not determine project name from root: {projectRoot}")).ToLower();

            _baseServices.Logger.Log(new ProjectDiscoveredLogEntry(projectRoot, context.ScriptPath));

            var project = new Project(_baseServices.Logger, projectName, projectRoot, _directories.RootDir, workspace);
            var projectBridge = new ProjectScriptBridge(_baseServices.Logger, _baseServices.Profiler, project);

            workspace.Projects.Add(projectName, project);

            context.ApplyEnum<EPlatform>();
            context.ApplyEnum<EArchitecture>();

            context.Init(new(
                _baseServices, services, context,
                workspaceBridge, workspace,
                projectBridge, project
            ), projectBridge, project);
            try {
                await context.ExecuteAsync(services.FileHashCache, project);
            } catch {
                var ex = new ManilaException($"Failed to execute project script: {context.ScriptPath}");
                throw ex;
            }

            _baseServices.Logger.Log(new ProjectInitializedLogEntry(project));

            return project;
        }
    }
    /// <summary>
    /// Executes the workspace script (Manila.js in the root directory).
    /// </summary>
    public async Task<Workspace> RunWorkspaceScriptAsync(ServiceContainer services, ScriptContext context) {
        using (new ProfileScope(_baseServices.Profiler, "Running Workspace Script")) {
            var workspaceRoot = Path.GetDirectoryName(context.ScriptPath) ?? throw new ManilaException($"Could not determine workspace root from script path: {context.ScriptPath}");
            _baseServices.Logger.Debug("Running workspace script: " + context.ScriptPath);

            var workspace = new Workspace(_baseServices.Logger, workspaceRoot);
            var workspaceBridge = new WorkspaceScriptBridge(_baseServices.Logger, _baseServices.Profiler, workspace);

            context.ApplyEnum<EArchitecture>();
            context.ApplyEnum<EPlatform>();

            context.Init(
                new(
                    _baseServices, services, context,
                    workspaceBridge, workspace,
                    null, null
                ), workspaceBridge, workspace
            );
            try {
                await context.ExecuteAsync(
                    services.FileHashCache, workspace
                );

                return workspace;
            } catch (Exception e) {
                var ex = new ManilaException($"Failed to execute workspace script: {context.ScriptPath}", e);
                throw ex;
            }
        }
    }

    /// <summary>
    /// Constructs the execution graph.
    /// </summary>
    public ExecutionGraph CreateExecutionGraph(ServiceContainer services, BaseServiceCotnainer baseServices, Workspace workspace) {
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
                    dependencies.Add(services.JobRegistry.GetJob(d) ?? throw new ManilaException($"Dependency '{d}' not found for job '{t.Name}'"));
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
            var layers = graph.GetExecutionLayers(jobID);
            _baseServices.Logger.Log(new BuildLayersLogEntry(layers));

            try {
                int layerIndex = 0;
                foreach (var layer in layers) {
                    using (new ProfileScope(_baseServices.Profiler, $"Executing Layer {layerIndex}")) {
                        Guid layerContextID = Guid.NewGuid();
                        _baseServices.Logger.Log(new BuildLayerStartedLogEntry(layer, layerContextID, layerIndex));
                        using (_baseServices.Logger.LogContext.PushContext(layerContextID)) {
                            List<Task> layerJobs = [];

                            foreach (var o in layer.Items) {
                                layerJobs.Add(Task.Run(() => o.ExecuteAsync()));
                            }

                            await Task.WhenAll(layerJobs);
                            _baseServices.Logger.Log(new BuildLayerCompletedLogEntry(layer, layerContextID, layerIndex));
                        }

                        layerIndex++;
                    }
                }
                _baseServices.Logger.Log(new BuildCompletedLogEntry(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startTime));
            } catch (Exception e) {
                var ex = new BuildException(e.Message, e);
                _baseServices.Logger.Log(new BuildFailedLogEntry(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startTime, e));
                throw ex;
            }
        }
    }
}
