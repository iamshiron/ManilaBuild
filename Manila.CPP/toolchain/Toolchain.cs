using Shiron.Manila.API;

namespace Shiron.Manila.CPP.Toolchain;

public abstract class Toolchain {
    public abstract void Build(Workspace workspace, Project project, BuildConfig config);
}
