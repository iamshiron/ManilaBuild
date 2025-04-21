namespace Shiron.Manila.CPP.Components;

using Shiron.Manila.API;
using Shiron.Manila.Ext;
using Shiron.Manila.Attributes;
using Shiron.Manila.CPP.Toolchain.Impl;
using Shiron.Manila.CPP.Toolchain;

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
    public List<string> IncludeDirs { get; set; } = [];
    public List<string> Links { get; set; } = [];

    public override void Build(Workspace workspace, Project project, BuildConfig config) {
        foreach (var dep in project._dependencies) {
            dep.Resolve(project);
        }

        Toolchain toolchain =
            ToolChain == EToolChain.Clang ? new ToolchainClang(workspace, project, config) :
            ToolChain == EToolChain.MSVC ? new ToolchainMSVC(workspace, project, config) :
            throw new Exception($"Toolchain {ToolChain} is not supported.");

        toolchain.Build(workspace, project, config);
    }

    public override void Run(Project project) {
        throw new Exception("A CppComponent cannot be started directly. Please use a ConsoleComponent.");
    }
}
