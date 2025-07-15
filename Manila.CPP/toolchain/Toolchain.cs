using Shiron.Manila.API;

namespace Shiron.Manila.CPP.Toolchain;

public abstract class Toolchain(Workspace workspace, Project project, BuildConfig config) {
    public readonly Workspace Workspace = workspace;
    public readonly Project Project = project;
    public readonly BuildConfig Config = config;

    public static readonly string[] HeaderFileExtensions = [".h", ".hpp", ".hxx", ".h++", ".hh"];
    public static readonly string[] SourceFileExtensions = [".c", ".cc", ".cpp", ".cxx", ".c++", ".m", ".mm"];

    public abstract void Build(Workspace workspace, Project project, BuildConfig config);
}
