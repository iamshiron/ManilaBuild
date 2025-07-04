using Shiron.Manila.API;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Ext;
using Shiron.Manila.Logging;
using Shiron.Manila.Utils;
using Spectre.Console;

namespace Shiron.Manila;

public sealed class ManilaEngine {
    internal static ManilaEngine? instance = null;
    public static ManilaEngine GetInstance() { if (instance == null) instance = new ManilaEngine(); return instance; }

    public string RootDir { get; private set; }
    public Workspace? Workspace { get; }
    public Project? CurrentProject { get; private set; }
    public ScriptContext? CurrentContext { get; private set; }
    public ScriptContext WorkspaceContext { get; }
    public string DataDir { get; private set; }
    public bool verboseLogger = false;
    public readonly long EngineCreatedTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public ExecutionGraph ExecutionGraph = new();

    public static readonly string VERSION = "0.0.0";

    private ManilaEngine() {
        RootDir = Directory.GetCurrentDirectory();
        Workspace = new Workspace(RootDir);
        WorkspaceContext = new ScriptContext(this, Workspace, Path.Join(RootDir, "Manila.js"));
        DataDir = Path.Join(RootDir, ".manila");
    }

    /// <summary>
    /// Main entry point for the engine. Runs the workspace script and all project scripts.
    /// </summary>
    public void Run() {
        if (!File.Exists("Manila.js")) {
            Logger.Error("No Manila.js file found in the current directory.");
            return;
        }

        Logger.Log(new EngineStartedLogEntry(RootDir, DataDir));

        var workspaceScript = Path.Join(RootDir, "Manila.js");
        var files = Directory.GetFiles(".", "Manila.js", SearchOption.AllDirectories)
            .Where(f => !Path.GetFullPath(f).Equals(Path.GetFullPath("Manila.js")))
            .ToList();

        try {
            RunWorkspaceScript();
            foreach (var script in files) {
                RunProjectScript(script);
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
                            // Applying duplicate enums is already handled in the ApplyEnum method.
                            WorkspaceContext.ApplyEnum(e);
                        }
                    }
                    f.Item2.Invoke(p);
                }
            }
        }

        Logger.Log(new ProjectsInitializedLogEntry(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - EngineCreatedTime));
    }

    /// <summary>
    /// Runs a project script.
    /// </summary>
    /// <param name="path">The relative path from the root</param>
    public void RunProjectScript(string path) {
        var projectRoot = Path.GetDirectoryName(Path.Join(Directory.GetCurrentDirectory(), path));
        var scriptPath = Path.Join(Directory.GetCurrentDirectory(), path);
        var projectName = Path.GetRelativePath(Directory.GetCurrentDirectory(), projectRoot).ToLower().Replace(Path.DirectorySeparatorChar, ':');

        Logger.Log(new ProjectDiscoveredLogEntry(projectRoot!, scriptPath));

        CurrentProject = new Project(projectName, projectRoot, Workspace);
        Workspace!.Projects.Add(projectName, CurrentProject);
        CurrentContext = new ScriptContext(this, CurrentProject, Path.Join(RootDir, path));

        CurrentContext.ApplyEnum<EPlatform>();
        CurrentContext.ApplyEnum<EArchitecture>();

        CurrentContext.Init();
        CurrentContext.Execute();

        Logger.Log(new ProjectInitializedLogEntry(CurrentProject));

        CurrentProject = null;
        CurrentContext = null;
    }
    /// <summary>
    /// Runs the workspace script. Always Manila.js in the root directory.
    /// </summary>
    public void RunWorkspaceScript() {
        Logger.Debug("Running workspace script: " + WorkspaceContext.ScriptPath);

        WorkspaceContext.ApplyEnum<EPlatform>();
        WorkspaceContext.ApplyEnum<EArchitecture>();

        WorkspaceContext.Init();
        try {
            WorkspaceContext.Execute();
        } catch {
            throw;
        }
    }

    public void ExecuteBuildLogic(string taskID) {
        // Add all existing tasks to the graph, hopefully I'll find a better solution for this in the future
        foreach (var t in Workspace.Tasks) {
            List<ExecutableObject> dependencies = [];
            foreach (var d in t.Dependencies) {
                dependencies.Add(Workspace.GetTask(d));
            }
            ExecutionGraph.Attach(t, dependencies);
        }
        foreach (var p in Workspace.Projects.Values) {
            foreach (var t in p.Tasks) {
                List<ExecutableObject> dependencies = [];
                foreach (var d in t.Dependencies) {
                    dependencies.Add(Workspace.GetTask(d));
                }
                ExecutionGraph.Attach(t, dependencies);
            }
        }

        var layers = ExecutionGraph.GetExecutionLayers(taskID);

        Logger.Log(new BuildLayersLogEntry(layers));

        long startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Logger.Log(new BuildStartedLogEntry());

        try {
            foreach (var layer in layers) {
                Guid layerContextID = Guid.NewGuid();
                Logger.Log(new BuildLayerStartedLogEntry(layer, layerContextID));
                var oldID = LogContext.CurrentContextId;
                LogContext.CurrentContextId = layerContextID;

                List<System.Threading.Tasks.Task> layerTasks = [];

                foreach (var o in layer.Items) {
                    layerTasks.Add(System.Threading.Tasks.Task.Run(() => {
                        if (o is API.Task task) {
                            try {
                                o.Execute();
                            } catch (Exception e) {
                                throw;
                            }
                        } else {
                            o.Execute();
                        }
                    }));
                }

                System.Threading.Tasks.Task.WaitAll(layerTasks);
                Logger.Log(new BuildLayerCompletedLogEntry(layer, layerContextID));
                LogContext.CurrentContextId = oldID;
            }
            Logger.Log(new BuildCompletedLogEntry(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startTime));
        } catch (Exception e) {
            Logger.Log(new BuildFailedLogEntry(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startTime, e));
        }
    }
}
