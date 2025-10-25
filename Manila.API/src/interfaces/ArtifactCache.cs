using System.Threading.Tasks;
using Shiron.Manila.API.Interfaces.Artifacts;
using Shiron.Manila.Interfaces;

namespace Shiron.Manila.API.Interfaces;

public interface IArtifactCache {
    // Lifecycle
    void LoadCache();
    void FlushCacheToDisk();

    // Cache operations
    Task CacheArtifactAsync(ICreatedArtifact artifact, BuildConfig config, Project project, ArtifactOutput output);
    Task<ICreatedArtifact> AppendCachedDataAsync(ICreatedArtifact artifact, BuildConfig config, Project project);
    void UpdateCacheAccessTime(BuildExitCodeCached cachedExitCode);

    // Queries
    bool IsCached(string fingerprint);
    ArtifactOutput GetMostRecentOutputForProject(Project project);
}
