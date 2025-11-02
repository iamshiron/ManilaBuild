
using Shiron.Manila.API;
using Shiron.Manila.API.Interfaces.Artifacts;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Interfaces;
using Shiron.Manila.JS;
using Shiron.Manila.Utils;
using Spectre.Console;

namespace Shiron.Manila.JS.Artifacts;

public class JSArtifact : IArtifactBuildable, IArtifactTransientExecutable {
    public Type BuildConfigType => typeof(JSBuildConfig);

    public IBuildExitCode Build(ArtifactOutputBuilder builder, Project project, BuildConfig config) {
        var instance = ManilaJS.Instance ?? throw new ManilaException("ManilaJS plugin instance is null.");
        instance.Info("Building JavaScript Artifact...");

        return new BuildExitCodeSuccess(builder);
    }

    public IExitCode ExecuteTransient(Project project, BuildConfig config) {
        var instance = ManilaJS.Instance ?? throw new ManilaException("ManilaJS plugin instance is null.");
        var jsConfig = (JSBuildConfig) config;

        var executable = ManilaJS.GetExecutableFromRuntime(jsConfig.Runtime);
        var sourceSet = project.SourceSets["main"] ?? throw new ManilaException("Main source set not found in project.");
        var entryFile = sourceSet.FileHandles.First() ?? throw new ManilaException("No source files found in main source set.");
        var file = Path.GetRelativePath(Directory.GetCurrentDirectory(), entryFile.Get());

        instance.Info($"Loading JavaScript file: {file}...");
        instance.Info($"Executing JavaScript Artifact transiently with runtime {jsConfig.Runtime}...");
        _ = instance.RunCommand(executable, [file], null);

        return new ExitCodeSuccess();
    }
}
