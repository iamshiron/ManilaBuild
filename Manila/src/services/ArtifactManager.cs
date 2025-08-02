
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Shiron.Manila.API;
using Shiron.Manila.API.Interfaces;
using Shiron.Manila.API.Interfaces.Artifacts;
using Shiron.Manila.API.Utils;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Interfaces;
using Shiron.Manila.Logging;
using Shiron.Manila.Profiling;
using Shiron.Manila.Utils;

namespace Shiron.Manila.Services;

public class ArtifactManager(ILogger logger, IProfiler profiler, string artifactsDir, string artifactsCacheFile) : IArtifactManager {
    private readonly ILogger _logger = logger;
    private readonly IProfiler _profiler = profiler;

    public readonly string ArtifactsDir = artifactsDir;
    public readonly string ArtifactsCacheFile = artifactsCacheFile;

    private Dictionary<string, ArtifactCacheEntry> _artifacts = [];
    private Task<bool>? _cacheLoadTask;
    private bool _cacheLoaded = false;

    private static readonly JsonSerializerSettings _jsonSettings = new() {
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Include,
        DefaultValueHandling = DefaultValueHandling.Include,
        TypeNameHandling = TypeNameHandling.Objects,
        TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
    };

    public string GetArtifactRoot(BuildConfig config, Project project, ICreatedArtifact artifact) {
        return Path.Join(
            ArtifactsDir,
            $"{PlatformUtils.GetPlatformKey()}-{PlatformUtils.GetArchitectureKey()}",
            $"{project.Name}-{artifact.Name}",
            artifact.GetFingerprint(config),
            config.GetArtifactKey()
        );
    }

    public async Task CacheArtifactAsync(ICreatedArtifact artifact, BuildConfig config, Project project) {
        if (_cacheLoadTask == null) throw new ManilaException("Cache load task is not initialized. Please call LoadCache() before caching artifacts.");
        _ = await _cacheLoadTask;

        _artifacts[GetArtifactRoot(config, project, artifact)] = ArtifactCacheEntry.FromArtifact(this, artifact, config, project);
    }

    public async Task<ICreatedArtifact> AppendCachedDataAsync(ICreatedArtifact artifact, BuildConfig config, Project project) {
        if (_cacheLoadTask == null) throw new ManilaException("Cache load task is not initialized. Please call LoadCache() before caching artifacts.");
        _ = await _cacheLoadTask;

        var root = GetArtifactRoot(config, project, artifact);
        if (_artifacts.TryGetValue(root, out var entry)) {
            artifact.LogCache = entry.LogCache;
        } else {
            _logger.Warning($"No cached data found for artifact {artifact.Name} in {root}");
        }
        return artifact;
    }

    public void LoadCache() {
        _cacheLoadTask = PerformCacheLoadAsync();
    }

    private async Task<bool> PerformCacheLoadAsync() {
        using (new ProfileScope(_profiler, "Background loading artifacts cache")) {
            try {
                if (_cacheLoaded) _logger.Warning("Cache is already loaded. Overwriting existing cache.");

                _cacheLoaded = true;
                if (!File.Exists(ArtifactsCacheFile)) return false;

                var json = await File.ReadAllTextAsync(ArtifactsCacheFile);
                _artifacts.Clear();
                _artifacts = JsonConvert.DeserializeObject<Dictionary<string, ArtifactCacheEntry>>(
                    json,
                    _jsonSettings
                ) ?? new Dictionary<string, ArtifactCacheEntry>();

                return true;
            } catch (Exception ex) {
                var e = new ManilaException($"Failed to load artifacts cache from {ArtifactsCacheFile}: {ex.Message}", ex);
                throw e;
            }
        }
    }

    public void FlushCacheToDisk() {
        if (_artifacts.Keys.Count == 0) {
            _logger.Debug("No artifacts to flush to disk, skipping.");
            return;
        }

        var dir = Path.GetDirectoryName(ArtifactsCacheFile);
        if (dir is not null && !Directory.Exists(dir)) _ = Directory.CreateDirectory(dir);

        File.WriteAllText(
            ArtifactsCacheFile,
            JsonConvert.SerializeObject(
                _artifacts,
                _jsonSettings
            )
        );
    }

    public IBuildExitCode BuildFromDependencies(IArtifactBuildable builder, ICreatedArtifact createdArtifact, Project project, BuildConfig config) {
        return builder.Build(new(GetArtifactRoot(config, project, createdArtifact)), project, config);
    }
}

public class ArtifactCacheEntry(string artifactRoot, long createdAt, long lastAccessed, long size, LogCache logCache) {
    public string ArtifactRoot { get; set; } = artifactRoot;
    public long CreatedAt { get; set; } = createdAt;
    public long LastAccessed { get; set; } = lastAccessed;
    public long Size { get; set; } = size;
    public LogCache LogCache { get; set; } = logCache;

    public static ArtifactCacheEntry FromArtifact(IArtifactManager artifactManager, ICreatedArtifact artifact, BuildConfig config, Project project) {
        return new(
            artifactManager.GetArtifactRoot(config, project, artifact),
            TimeUtils.Now(),
            TimeUtils.Now(),
            -1, // Size not implemented yet
            artifact.LogCache ?? throw new ManilaException(
                "Artifact does not have a log cache. Please ensure the artifact is properly executed before caching it."
            )
        );
    }
}
