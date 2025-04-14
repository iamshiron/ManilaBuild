using Shiron.Manila.API;

namespace Shiron.Manila.CPP.Toolchain;

public abstract class Toolchain(Workspace workspace, Project project, BuildConfig config) {
    public readonly Workspace workspace = workspace;
    public readonly Project project = project;
    public readonly BuildConfig config = config;

    public static readonly string[] headerFileExtensions = [".h", ".hpp", ".hxx", ".h++", ".hh"];
    public static readonly string[] sourceFileExtensions = [".c", ".cc", ".cpp", ".cxx", ".c++", ".m", ".mm"];

    public abstract void Build(Workspace workspace, Project project, BuildConfig config);
}
