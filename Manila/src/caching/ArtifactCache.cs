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
        _logger.Debug("LoadCache invoked; starting background cache load task.");
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
            _logger.Debug($"Loaded {_artifacts.Count} cached artifact entries from '{ArtifactsCacheFile}'.");
            return true;
        } catch (Exception ex) {
            var e = new ManilaException($"Failed to load artifacts cache from {ArtifactsCacheFile}: {ex.Message}", ex);
            throw e;
        }
    }

    public void FlushCacheToDisk() {
        _logger.Debug($"Flushing artifacts cache to disk at '{ArtifactsCacheFile}' (entries: {_artifacts.Keys.Count})...");

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
        _logger.Debug("Flush completed.");
    }

    public async Task CacheArtifactAsync(ICreatedArtifact artifact, BuildConfig config, Project project, ArtifactOutput output) {
        _logger.Debug($"Caching artifact '{artifact.Name}' for project '{project.Name}'.");
        if (_cacheLoadTask == null) throw new ManilaException("Cache load task is not initialized. Please call LoadCache() before caching artifacts.");
        _ = await _cacheLoadTask;

        if (artifact.ArtifactType is null)
            throw new ManilaException("Artifact does not have an associated ArtifactType. Please ensure the artifact is properly built before caching it.");
        var fingerprint = artifact.GetFingerprint(project, config);
        var entry = ArtifactCacheEntry.FromArtifact(this, artifact, config, project, output, artifact.ArtifactType);
        _artifacts[fingerprint] = entry;
        _logger.Debug($"Cached artifact. Fingerprint='{fingerprint}', Root='{entry.ArtifactRoot}', Files={output.FilePaths.Length}.");
    }

    public void UpdateCacheAccessTime(BuildExitCodeCached cachedExitCode) {
        _logger.Debug($"Updating access time for cached artifact. Fingerprint='{cachedExitCode.CacheKey}'.");
        if (_artifacts.TryGetValue(cachedExitCode.CacheKey, out var entry)) {
            entry.LastAccessed = TimeUtils.Now();
            _logger.Debug("Access time updated.");
        } else {
            _logger.Debug($"No entry found for '{cachedExitCode.CacheKey}' when updating access time.");
        }
    }

    public async Task<ICreatedArtifact> AppendCachedDataAsync(ICreatedArtifact artifact, BuildConfig config, Project project) {
        _logger.Debug($"Appending cached data to artifact '{artifact.Name}' (project '{project.Name}').");
        if (_cacheLoadTask == null) throw new ManilaException("Cache load task is not initialized. Please call LoadCache() before caching artifacts.");
        _ = await _cacheLoadTask;

        var fingerprint = artifact.GetFingerprint(project, config);
        if (_artifacts.TryGetValue(fingerprint, out var entry)) {
            artifact.LogCache = entry.LogCache;
            _logger.Debug($"Found cached log for fingerprint '{fingerprint}' (created {entry.CreatedAt}, last accessed {entry.LastAccessed}).");
        } else {
            _logger.Debug($"No cached log found for fingerprint '{fingerprint}'.");
        }
        return artifact;
    }

    public bool IsCached(string fingerprint) => _artifacts.ContainsKey(fingerprint);

    public ArtifactOutput GetMostRecentOutputForProject(Project project) {
        var name = project.Name;
        _logger.Debug($"Retrieving most recent output for project '{name}'.");
        List<Tuple<ArtifactCacheEntry, long>> candidates = new();
        foreach (var (key, entry) in _artifacts.OrderByDescending(kv => kv.Value.LastAccessed)) {
            if (key.StartsWith($"{name}-")) {
                candidates.Add(Tuple.Create(entry, entry.LastAccessed));
            }
        }
        _logger.Debug($"Found {candidates.Count} candidate cached entries for project '{name}'.");
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
