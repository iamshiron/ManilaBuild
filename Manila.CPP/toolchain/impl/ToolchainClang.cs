using Shiron.Manila.API;
using Shiron.Manila.CPP.Components;
using Shiron.Manila.Utils;

namespace Shiron.Manila.CPP.Toolchain.Impl;

public class ToolchainClang : Toolchain {
    public ToolchainClang(Workspace workspace, Project project, BuildConfig config) : base(workspace, project, config) {
    }

    public override void Build(Workspace workspace, Project project, BuildConfig config) {
        throw new NotImplementedException();
    }
}
