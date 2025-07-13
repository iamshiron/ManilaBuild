using System.Reflection;
using System.Threading.Tasks;
using Shiron.Manila.API;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Ext;
using Shiron.Manila.Logging;
using Shiron.Manila.Profiling;
using Shiron.Manila.Utils;

namespace Shiron.Manila;

public sealed class ManilaEngine {
    private static ManilaEngine? _instance;

    /// <summary>
    /// Gets the singleton instance of the ManilaEngine.
    /// </summary>
    public static ManilaEngine GetInstance() {
        // Use null-coalescing assignment for a concise, thread-safe (in modern .NET) singleton initialization.
        _instance ??= new ManilaEngine();
        return _instance;
    }

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
    public bool verboseLogger = false;

    /// <summary>
    /// Gets the timestamp (in Unix milliseconds) when the engine was created.
    /// </summary>
    public readonly long EngineCreatedTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// <summary>
    /// Gets the execution graph for managing task dependencies.
    /// </summary>
    public ExecutionGraph ExecutionGraph { get; } = new();

    /// <summary>
    /// Gets the NuGet package manager.
    /// </summary>
    public NuGetManager NuGetManager { get; }

    /// <summary>
    /// Gets the version of the Manila engine.
    /// </summary>
    public static readonly string VERSION = "0.0.1";

    #endregion

    private ManilaEngine() {
        RootDir = Directory.GetCurrentDirectory();
        Workspace = new(RootDir);
        WorkspaceContext = new ScriptContext(this, Workspace, Path.Join(RootDir, "Manila.js"));
        DataDir = Path.Join(RootDir, ".manila");
        NuGetManager = new(Path.Join(DataDir, "nuget"));
    }

    /// <summary>
    /// Main entry point for the engine. Runs the workspace script and all project scripts.
    /// </summary>
    public async System.Threading.Tasks.Task Run() {
        if (!File.Exists("Manila.js")) {
            Logger.Error("No Manila.js file found in the current directory.");
            return;
        }

        Logger.Log(new EngineStartedLogEntry(RootDir, DataDir));

        var workspaceScript = Path.Join(RootDir, "Manila.js");
        var files = Directory.GetFiles(".", "Manila.js", SearchOption.AllDirectories)
            .Where(f => !Path.GetFullPath(f).Equals(Path.GetFullPath("Manila.js")))
            .ToList();

        using (new ProfileScope("Running Scripts")) {
            try {
                await RunWorkspaceScript();
                foreach (var script in files) {
                    await RunProjectScript(script);
                }
            } catch {
                throw;
            }

            foreach (var f in Workspace!.ProjectFilters) {
                foreach (var p in Workspace.Projects.Values) {
                    if (f.Item1.Predicate(p)) {
                        foreach (var type in p.plugins) {
                            var plugin = ExtensionManager.GetInstance().GetPlugin(type);
                            foreach (var e in plugin.Enums) {
                                WorkspaceContext.ApplyEnum(e);
                            }
                        }
                        f.Item2.Invoke(p);
                    }
                }
            }

            Logger.Log(new ProjectsInitializedLogEntry(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - EngineCreatedTime));
        }
    }

    /// <summary>
    /// Executes a specific project script.
    /// </summary>
    /// <param name="path">The relative path to the project script from the root directory.</param>
    public async System.Threading.Tasks.Task RunProjectScript(string path) {
        using (new ProfileScope(MethodBase.GetCurrentMethod()!)) {
            var projectRoot = Path.GetDirectoryName(Path.Join(Directory.GetCurrentDirectory(), path));
            var scriptPath = Path.Join(Directory.GetCurrentDirectory(), path);
            var safeProjectRoot = projectRoot ?? Directory.GetCurrentDirectory();
            var projectName = Path.GetRelativePath(Directory.GetCurrentDirectory(), safeProjectRoot).ToLower().Replace(Path.DirectorySeparatorChar, ':');

            Logger.Log(new ProjectDiscoveredLogEntry(projectRoot!, scriptPath));

            CurrentProject = new Project(projectName, projectRoot!, Workspace);
            Workspace!.Projects.Add(projectName, CurrentProject);
            CurrentContext = new ScriptContext(this, CurrentProject, Path.Join(RootDir, path));

            CurrentContext.ApplyEnum<EPlatform>();
            CurrentContext.ApplyEnum<EArchitecture>();

            CurrentContext.Init();
            try {
                await CurrentContext.ExecuteAsync();
            } catch {
                throw;
            }

            Logger.Log(new ProjectInitializedLogEntry(CurrentProject));

            CurrentProject = null;
            CurrentContext = null;
        }
    }
    /// <summary>
    /// Executes the workspace script (Manila.js in the root directory).
    /// </summary>
    public async System.Threading.Tasks.Task RunWorkspaceScript() {
        using (new ProfileScope(MethodBase.GetCurrentMethod()!)) {
            Logger.Debug("Running workspace script: " + WorkspaceContext.ScriptPath);

            WorkspaceContext.ApplyEnum<EPlatform>();
            WorkspaceContext.ApplyEnum<EArchitecture>();

            WorkspaceContext.Init();
            try {
                await WorkspaceContext.ExecuteAsync();
            } catch {
                throw;
            }
        }
    }

    /// <summary>
    /// Constructs the execution graph and runs the build logic for a specified task.
    /// </summary>
    /// <param name="taskID">The ID of the task to execute.</param>
    public void ExecuteBuildLogic(string taskID) {
        var task = GetTask(taskID);
        Logger.Debug($"Found task: {task}");

        // Add all existing tasks to the graph, hopefully I'll find a better solution for this in the future
        ExecutionGraph.ExecutionLayer[] layers = [];
        using (new ProfileScope("Building Dependency Tree")) {
            /*foreach (var t in Workspace.Tasks) {
                List<ExecutableObject> dependencies = [];
                foreach (var d in t.Dependencies) {
                    dependencies.Add(GetTask(d));
                }
                ExecutionGraph.Attach(t, dependencies);
            }
            foreach (var p in Workspace.Projects.Values) {
                foreach (var t in p.Tasks) {
                    List<ExecutableObject> dependencies = [];
                    foreach (var d in t.Dependencies) {
                        dependencies.Add(GetTask(d));
                    }
                    ExecutionGraph.Attach(t, dependencies);
                }
            }*/
            List<API.Task> Tasks = [.. Workspace.Tasks];
            foreach (var p in Workspace.Projects.Values) {
                Tasks.AddRange([.. p.Tasks]);
                foreach (var a in p.Artifacts.Values) {
                    Tasks.AddRange([.. a.Tasks]);
                }
            }

            foreach (var t in Tasks) {
                List<ExecutableObject> dependencies = [];
                foreach (var d in t.Dependencies) {
                    dependencies.Add(GetTask(d));
                }
                ExecutionGraph.Attach(t, dependencies);
            }

            layers = ExecutionGraph.GetExecutionLayers(taskID);
        }

        Logger.Log(new BuildLayersLogEntry(layers));

        long startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Logger.Log(new BuildStartedLogEntry());

        using (new ProfileScope("Executing Execution Layers")) {
            try {
                int layerIndex = 0;
                foreach (var layer in layers) {
                    using (new ProfileScope($"Executing Layer {layerIndex}")) {
                        Guid layerContextID = Guid.NewGuid();
                        Logger.Log(new BuildLayerStartedLogEntry(layer, layerContextID, layerIndex));
                        using (LogContext.PushContext(layerContextID)) {
                            List<System.Threading.Tasks.Task> layerTasks = [];

                            foreach (var o in layer.Items) {
                                layerTasks.Add(System.Threading.Tasks.Task.Run(() => o.Execute()));
                            }

                            System.Threading.Tasks.Task.WhenAll(layerTasks).GetAwaiter().GetResult();
                            Logger.Log(new BuildLayerCompletedLogEntry(layer, layerContextID, layerIndex));
                        }

                        layerIndex++;
                    }
                }
                Logger.Log(new BuildCompletedLogEntry(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startTime));
            } catch (Exception e) {
                var ex = new BuildException(e.Message, e);
                Logger.Log(new BuildFailedLogEntry(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startTime, e));
                throw ex;
            }
        }
    }

    public bool HasTask(string uri) {
        return GetTask(uri) != null;
    }

    public API.Task GetTask(string uri) {
        var info = RegexUtils.MatchTasks(uri);

        if (info.Project == null) {
            var temp = Workspace.Tasks.Find(m => m.Name == info.Task) ?? throw new ManilaException($"Task {uri} not found!");
            return temp;
        }

        var project = Workspace.Projects[info.Project];
        if (info.Artifact == null) {
            var temp = project.Tasks.Find(m => m.Name == info.Task) ?? throw new ManilaException($"Task {uri} not found!");
            return temp;
        }

        var t = project.Artifacts[info.Artifact].Tasks.First(t => t.Name == info.Task) ?? throw new ManilaException($"Task {uri} not found!");
        return t;
    }
    public bool TryGetTask(string uri, out API.Task? task) {
        try {
            var t = GetTask(uri);
            task = t;
            return t != null;
        } catch {
            task = null;
            return false;
        }
    }
}
