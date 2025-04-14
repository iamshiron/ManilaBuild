namespace Shiron.Manila.CPP.Components;

using Shiron.Manila.API;
using Shiron.Manila.Ext;
using Shiron.Manila.Attributes;

/// <summary>
/// Represents a C++ project.
/// </summary>
public class CppComponent : LanguageComponent {
    public CppComponent(string name) : base(name) {
    }

    [ScriptProperty]
    public Dir? BinDir { get; set; }
    [ScriptProperty]
    public Dir? ObjDir { get; set; }
    [ScriptProperty]
    public EToolChain? ToolChain { get; set; }

    public override void Build(Workspace workspace, Project project, BuildConfig config) {
        var instance = ManilaCPP.Instance;
        instance.Info($"Building {project.Name} with {ToolChain.name} toolchain.");
    }

    public override void Run(Workspace workspace, Project project, BuildConfig config) {
    }
}
