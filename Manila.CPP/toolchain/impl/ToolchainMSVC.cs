using Shiron.Manila.API;

namespace Shiron.Manila.CPP.Toolchain.Impl;

public class ToolchainMSVC : Toolchain {
    public ToolchainMSVC(Workspace workspace, Project project, BuildConfig config) : base(workspace, project, config) {
    }

    public override void Build(Workspace workspace, Project project, BuildConfig config) {
        var instance = ManilaCPP.Instance;
        instance.Info($"Building {project.Name} with MSVC toolchain.");
    }
}
