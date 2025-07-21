using System.Reflection;
using System.Threading.Tasks;
using Microsoft.ClearScript.V8;
using Shiron.Manila.API;
using Shiron.Manila.Artifacts;
using Shiron.Manila.Caching;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Ext;
using Shiron.Manila.Logging;
using Shiron.Manila.Profiling;
using Shiron.Manila.Registries;
using Shiron.Manila.Utils;

namespace Shiron.Manila;

public sealed class ManilaEngine {
    private static ManilaEngine? _instance;

    #region Properties

    /// <summary>
    /// Gets the root directory of the workspace.
    /// </summary>
    public string RootDir { get; }

    /// <summary>
    /// Gets the current workspace.
    /// </summary>
    public Workspace Workspace { get; }

    /// <summary>
    /// Gets the currently executing project. This is null if no project script is running.
    /// </summary>
    public Project? CurrentProject { get; private set; }

    /// <summary>
    /// Gets the script context for the currently executing project.
    /// </summary>
    public ScriptContext? CurrentContext { get; private set; }

    /// <summary>
    /// Gets the script context for the workspace.
    /// </summary>
    public ScriptContext WorkspaceContext { get; }

    /// <summary>
    /// Gets the directory for Manila's data files.
    /// </summary>
    public string DataDir { get; }

    // This is reverted to a public field to maintain API compatibility.
    public bool VerboseLogger = false;

    /// <summary>
    /// Gets the timestamp (in Unix milliseconds) when the engine was created.
    /// </summary>
    public readonly long EngineCreatedTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// <summary>
    /// Gets the execution graph for managing job dependencies.
    /// </summary>
    public ExecutionGraph ExecutionGraph { get; }

    /// <summary>
    /// Gets the NuGet package manager.
    /// </summary>
    public NuGetManager NuGetManager { get; }

    public readonly IJobRegistry JobRegisry = new JobRegistry();
    public readonly IArtifactManager ArtifactManager;
    public readonly IExtensionManager ExtensionManager;

    public readonly FileHashCache FileHashCache;

    /// <summary>
    /// Gets the version of the Manila engine.
    /// </summary>
    public static readonly string VERSION = "0.0.1";

    private readonly ILogger _logger;
    private readonly IProfiler _profiler;

    #endregion

    private static V8ScriptEngine CreateScriptEngine() {
        return new(
            V8ScriptEngineFlags.EnableTaskPromiseConversion
        ) {
            ExposeHostObjectStaticMembers = true,
        };
    }

    public ManilaEngine(ILogger logger, IProfiler profiler) {
        _logger = logger;
        _profiler = profiler;

        RootDir = Directory.GetCurrentDirectory();
        DataDir = Path.Join(RootDir, ".manila");

        NuGetManager = new(_logger, _profiler, Path.Join(DataDir, "nuget"));
        ExtensionManager = new ExtensionManager(_logger, _profiler, NuGetManager);
        ArtifactManager = new ArtifactManager(_logger, _profiler, Path.Join(DataDir, "artifacts"), Path.Join(DataDir, "cache", "artifacts.json"));
        WorkspaceContext = new(_logger, profiler, CreateScriptEngine(), RootDir, Path.Join(RootDir, "Manila.js"));
        Workspace = new(logger, RootDir);
        FileHashCache = new(Path.Join(DataDir, "cache", "filehashes.db"), RootDir);
        ExecutionGraph = new(_logger, _profiler);
    }

    /// <summary>
    /// Main entry point for the engine. Runs the workspace script and all project scripts.
    /// </summary>
    public async Task Run() {
        List<Task> tasks = [];
        List<Task> projectTasks = [];

        if (!File.Exists("Manila.js")) {
            _logger.Error("No Manila.js file found in the current directory.");
            return;
        }

        async Task LoadCacheAndLogResult() {
            var result = await ArtifactManager.LoadCache();
            if (result) {
                _logger.Info("Loaded artifacts cache from disk.");
            } else {
                _logger.Warning("No artifacts cache found, starting with an empty cache.");
            }
        }

        _logger.Log(new EngineStartedLogEntry(RootDir, DataDir));

        var workspaceScript = Path.Join(RootDir, "Manila.js");
        var files = Directory.GetFiles(".", "Manila.js", SearchOption.AllDirectories)
            .Where(f => !Path.GetFullPath(f).Equals(Path.GetFullPath("Manila.js")))
            .ToList();

        async Task RunAllScriptsAndProcessFiltersAsync() {
            using (new ProfileScope(_profiler, "Running Scripts")) {
                await RunWorkspaceScript();

                var files = Directory.GetFiles(".", "Manila.js", SearchOption.AllDirectories)
                    .Where(f => !Path.GetFullPath(f).Equals(Path.GetFullPath("Manila.js")))
                    .ToList();

                foreach (var file in files) {
                    await RunProjectScript(file);
                }

                await Task.Run(() => {
                    foreach (var f in Workspace!.ProjectFilters) {
                        foreach (var p in Workspace.Projects.Values) {
                            if (f.Item1.Predicate(p)) {
                                foreach (var type in p.Plugins) {
                                    var plugin = ExtensionManager.GetPlugin(type);
                                    foreach (var e in plugin.Enums) {
                                        WorkspaceContext.ApplyEnum(e);
                                    }
                                }
                                f.Item2.Invoke(p);
                            }
                        }
                    }
                });
            }
        }

        tasks.Add(LoadCacheAndLogResult());
        tasks.Add(RunAllScriptsAndProcessFiltersAsync());

        await Task.WhenAll(tasks);
        _logger.Log(new ProjectsInitializedLogEntry(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - EngineCreatedTime));
    }

    /// <summary>
    /// Executes a specific project script.
    /// </summary>
    /// <param name="path">The relative path to the project script from the root directory.</param>
    public async Task RunProjectScript(string path) {
        using (new ProfileScope(_profiler, MethodBase.GetCurrentMethod()!)) {
            var projectRoot = Path.GetDirectoryName(Path.Join(Directory.GetCurrentDirectory(), path)) ?? throw new ManilaException($"Failed to determine project root for script: {path}");
            var projectName = Path.GetRelativePath(Directory.GetCurrentDirectory(), projectRoot).ToLower().Replace(Path.DirectorySeparatorChar, ':');
            var scriptPath = Path.Join(Directory.GetCurrentDirectory(), path);

            _logger.Log(new ProjectDiscoveredLogEntry(projectRoot, scriptPath));

            CurrentProject = new(_logger, projectName, projectRoot, RootDir, Workspace);
            Workspace!.Projects.Add(projectName, CurrentProject);
            CurrentContext = new(_logger, _profiler, CreateScriptEngine(), RootDir, Path.Join(RootDir, path));

            CurrentContext.ApplyEnum<EPlatform>();
            CurrentContext.ApplyEnum<EArchitecture>();

            CurrentContext.Init(new(
                _logger, _profiler, JobRegisry, ArtifactManager, ExtensionManager, CurrentContext, CurrentProject, Workspace
            ), CurrentProject);
            try {
                await CurrentContext.ExecuteAsync(FileHashCache, CurrentProject);
            } catch {
                throw;
            }

            _logger.Log(new ProjectInitializedLogEntry(CurrentProject));

            CurrentProject = null;
            CurrentContext = null;
        }
    }
    /// <summary>
    /// Executes the workspace script (Manila.js in the root directory).
    /// </summary>
    public async Task RunWorkspaceScript() {
        using (new ProfileScope(_profiler, MethodBase.GetCurrentMethod()!)) {
            _logger.Debug("Running workspace script: " + WorkspaceContext.ScriptPath);

            WorkspaceContext.ApplyEnum<EArchitecture>();
            WorkspaceContext.ApplyEnum<EPlatform>();

            WorkspaceContext.Init(new(
                _logger, _profiler, JobRegisry, ArtifactManager, ExtensionManager, WorkspaceContext, CurrentProject, Workspace
            ), Workspace);
            try {
                await WorkspaceContext.ExecuteAsync(
                    FileHashCache, Workspace
                );
            } catch {
                throw;
            }
        }
    }

    /// <summary>
    /// Constructs the execution graph and runs the build logic for a specified job.
    /// </summary>
    /// <param name="jobID">The ID of the job to execute.</param>
    public void ExecuteBuildLogic(string jobID) {
        var job = JobRegisry.GetJob(jobID);
        _logger.Debug($"Found job: {job}");

        // Add all existing jobs to the graph, hopefully I'll find a better solution for this in the future
        ExecutionGraph.ExecutionLayer[] layers = [];
        using (new ProfileScope(_profiler, "Building Dependency Tree")) {
            List<Job> Jobs = [.. Workspace.Jobs];
            foreach (var p in Workspace.Projects.Values) {
                Jobs.AddRange([.. p.Jobs]);
                foreach (var a in p.Artifacts.Values) {
                    Jobs.AddRange([.. a.Jobs]);
                }
            }

            foreach (var t in Jobs) {
                List<ExecutableObject> dependencies = [];
                foreach (var d in t.Dependencies) {
                    dependencies.Add(JobRegisry.GetJob(d) ?? throw new ManilaException($"Dependency '{d}' not found for job '{t.Name}'"));
                }
                ExecutionGraph.Attach(t, dependencies);
            }

            layers = ExecutionGraph.GetExecutionLayers(jobID);
        }

        _logger.Log(new BuildLayersLogEntry(layers));

        long startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _logger.Log(new BuildStartedLogEntry());

        using (new ProfileScope(_profiler, "Executing Execution Layers")) {
            try {
                int layerIndex = 0;
                foreach (var layer in layers) {
                    using (new ProfileScope(_profiler, $"Executing Layer {layerIndex}")) {
                        Guid layerContextID = Guid.NewGuid();
                        _logger.Log(new BuildLayerStartedLogEntry(layer, layerContextID, layerIndex));
                        using (_logger.LogContext.PushContext(layerContextID)) {
                            List<Task> layerJobs = [];

                            foreach (var o in layer.Items) {
                                layerJobs.Add(Task.Run(() => o.Execute()));
                            }

                            Task.WhenAll(layerJobs).GetAwaiter().GetResult();
                            _logger.Log(new BuildLayerCompletedLogEntry(layer, layerContextID, layerIndex));
                        }

                        layerIndex++;
                    }
                }
                _logger.Log(new BuildCompletedLogEntry(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startTime));
            } catch (Exception e) {
                var ex = new BuildException(e.Message, e);
                _logger.Log(new BuildFailedLogEntry(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startTime, e));
                throw ex;
            }
        }
    }

    public void Dispose() {
        ArtifactManager.FlushCacheToDisk();

        _instance = null;
    }
}
