
using Shiron.Manila.API.Interfaces.Artifacts;
using Shiron.Manila.Interfaces;

namespace Shiron.Manila.API.Interfaces;

public interface IArtifactManager {
    string GetArtifactRoot(BuildConfig config, Project project, IArtifact artifact);
    Task CacheArtifactAsync(IArtifact artifact, BuildConfig config, Project project);
    Task<IArtifact> AppendCachedDataAsync(IArtifact artifact, BuildConfig config, Project project);
    void LoadCache();
    void FlushCacheToDisk();

    IBuildExitCode BuildArtifact(
        IArtifactBuilder artifactBuilder,
        IArtifact artifact,
        BuildConfig config,
        Project project,
        IArtifactOutput[] dependencies
    );
}
