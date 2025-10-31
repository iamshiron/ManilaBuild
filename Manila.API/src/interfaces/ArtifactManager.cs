
using Shiron.Manila.API.Interfaces.Artifacts;
using Shiron.Manila.Interfaces;

namespace Shiron.Manila.API.Interfaces;

public interface IArtifactManager {
    string GetArtifactRoot(BuildConfig config, Project project, ICreatedArtifact artifact);
    Task CacheArtifactAsync(ICreatedArtifact artifact, BuildConfig config, Project project, ArtifactOutput output);
    Task<ICreatedArtifact> AppendCachedDataAsync(ICreatedArtifact artifact, BuildConfig config, Project project);
    void LoadCache();
    void FlushCacheToDisk();
    void UpdateCacheAccessTime(BuildExitCodeCached cachedExitCode);

    IBuildExitCode BuildFromDependencies(IArtifactBlueprint artifact, ICreatedArtifact createdArtifact, Project project, BuildConfig config, bool invalidateCache);
    IExitCode RunTransient(IArtifactBlueprint artifact, Project project, BuildConfig config, string? sourceSet = null);
}
