using Shiron.Manila.API;
using Shiron.Manila.Ext;
using Shiron.Manila.Utils;

namespace Shiron.Manila;

public sealed class ManilaEngine {
    internal static ManilaEngine? instance = null;
    public static ManilaEngine GetInstance() { if (instance == null) instance = new ManilaEngine(); return instance; }

    public string Root { get; private set; }
    public Workspace? Workspace { get; }
    public Project? CurrentProject { get; private set; }
    public ScriptContext? CurrentContext { get; private set; }
    public ScriptContext WorkspaceContext { get; }

    public static readonly string VERSION = "0.0.0";

    private ManilaEngine() {
        Root = Directory.GetCurrentDirectory();
        Workspace = new Workspace(Root);
        WorkspaceContext = new ScriptContext(this, Workspace, Path.Join(Root, "Manila.js"));
    }

    /// <summary>
    /// Main entry point for the engine. Runs the workspace script and all project scripts.
    /// </summary>
    public void Run() {
        if (!System.IO.File.Exists("Manila.js")) {
            Logger.Error("No Manila.js file found in the current directory.");
            return;
        }

        var workspaceScript = Path.Join(Root, "Manila.js");
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
    }

    /// <summary>
    /// Runs a project script.
    /// </summary>
    /// <param name="path">The relative path from the root</param>
    public void RunProjectScript(string path) {
        Logger.Debug("Running project script: " + path);
        string projectPath = Path.GetDirectoryName(Path.GetRelativePath(Root, path));
        string name = projectPath.ToLower().Replace(Path.DirectorySeparatorChar, ':');

        CurrentProject = new API.Project(name, projectPath, Workspace);
        Workspace!.Projects.Add(name, CurrentProject);
        CurrentContext = new ScriptContext(this, CurrentProject, Path.Join(Root, path));

        CurrentContext.ApplyEnum(typeof(EPlatform));
        CurrentContext.ApplyEnum(typeof(EArchitecture));

        CurrentContext.Init();
        CurrentContext.Execute();

        CurrentProject = null;
        CurrentContext = null;
    }
    /// <summary>
    /// Runs the workspace script. Always Manila.js in the root directory.
    /// </summary>
    public void RunWorkspaceScript() {
        string path = "Manila.js";
        Logger.Debug("Running workspace script: " + path);

        WorkspaceContext.ApplyEnum<EPlatform>();
        WorkspaceContext.ApplyEnum<EArchitecture>();

        WorkspaceContext.Init();
        WorkspaceContext.Execute();
    }
}
