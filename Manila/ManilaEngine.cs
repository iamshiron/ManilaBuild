using Shiron.Manila.API;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Ext;
using Shiron.Manila.Logging;

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

        RunWorkspaceScript();
        foreach (var script in files) {
            RunProjectScript(script);
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
        Logger.Log(new ProjectDiscoveredLogEntry(Path.GetDirectoryName(path), path));
        string projectPath = Path.GetDirectoryName(Path.GetRelativePath(RootDir, path));
        string name = projectPath.ToLower().Replace(Path.DirectorySeparatorChar, ':');

        CurrentProject = new API.Project(name, projectPath, Workspace);
        Workspace!.Projects.Add(name, CurrentProject);
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
        WorkspaceContext.Execute();
    }

    public void ExecuteBuildLogic(string taskID) {
        var task = Workspace!.GetTask(taskID);

        var order = task.GetExecutionOrder();
        Logger.Debug("Execution Order: " + string.Join(", ", order));

        long startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Logger.Log(new BuildStartedLogEntry());
        foreach (var t in order) {
            var taskToRun = Workspace.GetTask(t);
            var taskContextID = Guid.NewGuid();

            try {
                if (taskToRun.Action == null) {
                    Logger.Warning("Task has no action: " + t);
                } else {
                    Logger.Log(new TaskExecutionStartedLogEntry(taskToRun, taskContextID));
                    taskToRun.Action.Invoke();
                    Logger.Log(new TaskExecutionFinishedLogEntry(taskToRun, taskContextID));
                }
            } catch (Exception e) {
                Logger.Log(new BuildFailedLogEntry(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startTime, e));
                throw new TaskFailedException(taskToRun, e);
            }
        }
        Logger.Log(new BuildCompletedLogEntry(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startTime));
    }
}
