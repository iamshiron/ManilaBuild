
using Shiron.Manila.API;
using Shiron.Manila.Artifacts;
using Shiron.Manila.Attributes;
using Shiron.Manila.CPP.Toolchain;
using Shiron.Manila.CPP.Toolchain.Impl;
using Shiron.Manila.Ext;

namespace Shiron.Manila.CPP.Components;
/// <summary>
/// Represents a C++ project.
/// </summary>
public class CppComponent(string name) : LanguageComponent(name, typeof(CPPBuildConfig)) {
    [ScriptProperty]
    public EToolChain? ToolChain { get; set; }
    public List<string> IncludeDirs { get; set; } = [];
    public List<string> Links { get; set; } = [];

    public override IBuildExitCode Build(Workspace workspace, Project project, BuildConfig config, Artifact artifact, IArtifactManager artifactManager) {
        foreach (var dep in project.Dependencies) {
            dep.Resolve(project);
        }

        Toolchain.Toolchain toolchain =
            ToolChain == EToolChain.Clang ? new ToolchainClang(workspace, project, config) :
            ToolChain == EToolChain.MSVC ? new ToolchainMSVC(workspace, project, config) :
            throw new Exception($"Toolchain '{ToolChain}' is not supported.");

        toolchain.Build(workspace, project, config);

        return new BuildExitCodeSuccess();
    }

    public override void Run(Project project) {
        throw new Exception("A CppComponent cannot be started directly. Please use a ConsoleComponent.");
    }
}
