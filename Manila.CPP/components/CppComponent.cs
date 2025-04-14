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

    public override void Build(Workspace workspace, Project project, BuildConfig config) {
        Toolchain toolchain =
            ToolChain == EToolChain.Clang ? new ToolchainClang() :
            ToolChain == EToolChain.MSVC ? new ToolchainMSVC() :
            throw new NotImplementedException("Toolchain not implemented.");

        toolchain.Build(workspace, project, config);
    }

    public override void Run(Workspace workspace, Project project, BuildConfig config) {
    }
}
