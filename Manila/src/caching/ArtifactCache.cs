using System.Collections.Concurrent;
using Newtonsoft.Json;
using Shiron.Manila.API;
using Shiron.Manila.API.Exceptions;
using Shiron.Manila.API.Interfaces;
using Shiron.Manila.API.Interfaces.Artifacts;
using Shiron.Manila.API.Utils;
using Shiron.Manila.Interfaces;
using Shiron.Manila.Logging;
using Shiron.Manila.Utils;

namespace Shiron.Manila.Caching;

public class ArtifactCache(ILogger logger, string artifactsDir, string artifactsCacheFile) : IArtifactCache {
    private readonly ILogger _logger = logger;

    public readonly string ArtifactsDir = artifactsDir;
    public readonly string ArtifactsCacheFile = artifactsCacheFile;

    // The key is the artifact fingerprint
    private ConcurrentDictionary<string, ArtifactCacheEntry> _artifacts = new();
    private Task<bool>? _cacheLoadTask;
    private bool _cacheLoaded = false;

    private static readonly JsonSerializerSettings _jsonSettings = new() {
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Include,
        DefaultValueHandling = DefaultValueHandling.Include,
        TypeNameHandling = TypeNameHandling.Objects,
        TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
    };

    public void LoadCache() {
        _cacheLoadTask = PerformCacheLoadAsync();
    }

    private async Task<bool> PerformCacheLoadAsync() {
        _logger.Debug("Loading artifacts cache from disk...");

        try {
            if (_cacheLoaded) _logger.Warning("Cache is already loaded. Overwriting existing cache.");

            _cacheLoaded = true;
            if (!File.Exists(ArtifactsCacheFile)) return false;

            var json = await File.ReadAllTextAsync(ArtifactsCacheFile);
            _artifacts.Clear();
            _artifacts = JsonConvert.DeserializeObject<ConcurrentDictionary<string, ArtifactCacheEntry>>(
                json,
                _jsonSettings
            ) ?? [];

            return true;
        } catch (Exception ex) {
            var e = new ManilaException($"Failed to load artifacts cache from {ArtifactsCacheFile}: {ex.Message}", ex);
            throw e;
        }
    }

    public void FlushCacheToDisk() {
        _logger.Debug("Flushing artifacts cache to disk...");

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

    public async Task CacheArtifactAsync(ICreatedArtifact artifact, BuildConfig config, Project project, ArtifactOutput output) {
        _logger.Debug($"Caching artifact {artifact.Name}...");
        if (_cacheLoadTask == null) throw new ManilaException("Cache load task is not initialized. Please call LoadCache() before caching artifacts.");
        _ = await _cacheLoadTask;

        if (artifact.ArtifactType is null)
            throw new ManilaException("Artifact does not have an associated ArtifactType. Please ensure the artifact is properly built before caching it.");

        _artifacts[artifact.GetFingerprint(project, config)] = ArtifactCacheEntry.FromArtifact(this, artifact, config, project, output, artifact.ArtifactType);
    }

    public void UpdateCacheAccessTime(BuildExitCodeCached cachedExitCode) {
        _logger.Debug($"Updating access time for cached artifact with fingerprint {cachedExitCode.CacheKey}...");
        if (_artifacts.TryGetValue(cachedExitCode.CacheKey, out var entry)) {
            entry.LastAccessed = TimeUtils.Now();
        } else {
            _logger.Debug($"No cached artifact found for fingerprint {cachedExitCode.CacheKey} to update access time.");
        }
    }

    public async Task<ICreatedArtifact> AppendCachedDataAsync(ICreatedArtifact artifact, BuildConfig config, Project project) {
        _logger.Debug($"Appending cached data to artifact {artifact.Name}...");
        if (_cacheLoadTask == null) throw new ManilaException("Cache load task is not initialized. Please call LoadCache() before caching artifacts.");
        _ = await _cacheLoadTask;

        var fingerprint = artifact.GetFingerprint(project, config);
        if (_artifacts.TryGetValue(fingerprint, out var entry)) {
            artifact.LogCache = entry.LogCache;
        } else {
            _logger.Debug($"No cached data found for artifact {artifact.Name} in {fingerprint}");
        }
        return artifact;
    }

    public bool IsCached(string fingerprint) => _artifacts.ContainsKey(fingerprint);

    public ArtifactOutput GetMostRecentOutputForProject(Project project) {
        var name = project.Name;
        List<Tuple<ArtifactCacheEntry, long>> candidates = new();
        foreach (var (key, entry) in _artifacts.OrderByDescending(kv => kv.Value.LastAccessed)) {
            if (key.StartsWith($"{name}-")) {
                candidates.Add(Tuple.Create(entry, entry.LastAccessed));
            }
        }

        return candidates.Count == 0
            ? throw new ManilaException($"No cached artifacts found for project '{name}'.")
            : candidates.OrderByDescending(t => t.Item2).First().Item1.Output;
    }

    internal string GetArtifactRoot(BuildConfig config, Project project, ICreatedArtifact artifact) {
        return Path.Join(
            ArtifactsDir,
            $"{PlatformUtils.GetPlatformKey()}-{PlatformUtils.GetArchitectureKey()}",
            $"{project.Name}-{artifact.Name}",
            artifact.GetFingerprint(project, config),
            config.GetArtifactKey()
        );
    }

    internal class ArtifactCacheEntry(string artifactRoot, string fingerprint, long createdAt, long lastAccessed, long size, LogCache logCache, ArtifactOutput output, IArtifactBlueprint artifactType) {
        public string ArtifactRoot { get; set; } = artifactRoot;
        public string Fringerprint { get; } = fingerprint;
        public long CreatedAt { get; set; } = createdAt;
        public long LastAccessed { get; set; } = lastAccessed;
        public long Size { get; set; } = size;
        public LogCache LogCache { get; set; } = logCache;
        public ArtifactOutput Output { get; set; } = output;
        public IArtifactBlueprint ArtifactType { get; set; } = artifactType;

        public static ArtifactCacheEntry FromArtifact(ArtifactCache cache, ICreatedArtifact artifact, BuildConfig config, Project project, ArtifactOutput output, IArtifactBlueprint artifactType) {
            return new(
                cache.GetArtifactRoot(config, project, artifact),
                artifact.GetFingerprint(project, config),
                TimeUtils.Now(),
                TimeUtils.Now(),
                -1,
                artifact.LogCache ?? throw new ManilaException(
                    "Artifact does not have a log cache. Please ensure the artifact is properly executed before caching it."
                ),
                output,
                artifactType
            );
        }
    }
}
