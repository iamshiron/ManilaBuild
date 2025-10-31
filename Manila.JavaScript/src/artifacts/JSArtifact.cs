
using Shiron.Manila.API;
using Shiron.Manila.API.Interfaces.Artifacts;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Interfaces;
using Shiron.Manila.JS;

namespace Shiron.Manila.JS.Artifacts;

public class JSArtifact : IArtifactBuildable, IArtifactTransientExecutable {
    public Type BuildConfigType => typeof(JSBuildConfig);

    public IBuildExitCode Build(ArtifactOutputBuilder builder, Project project, BuildConfig config) {
        var instance = ManilaJS.Instance ?? throw new ManilaException("ManilaJS plugin instance is null.");
        instance.Info("Building JavaScript Artifact...");

        return new BuildExitCodeSuccess(builder);
    }
}
