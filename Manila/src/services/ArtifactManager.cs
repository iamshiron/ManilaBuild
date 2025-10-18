
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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

    /// <summary>
    /// Stores the cached artifacts.
    /// The key is the artifact fingerprint.
    /// </summary>
    private ConcurrentDictionary<string, ArtifactCacheEntry> _artifacts = new();
    private Task<bool>? _cacheLoadTask;
    private bool _cacheLoaded = false;
    // Prevents concurrent duplicate builds of the same artifact fingerprint
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _buildLocks = new();

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
            artifact.GetFingerprint(project, config),
            config.GetArtifactKey()
        );
    }

    public async Task CacheArtifactAsync(ICreatedArtifact artifact, BuildConfig config, Project project, ArtifactOutput output) {
        if (_cacheLoadTask == null) throw new ManilaException("Cache load task is not initialized. Please call LoadCache() before caching artifacts.");
        _ = await _cacheLoadTask;

        _artifacts[artifact.GetFingerprint(project, config)] = ArtifactCacheEntry.FromArtifact(this, artifact, config, project, output);
    }

    public void UpdateCacheAccessTime(BuildExitCodeCached cachedExitCode) {
        if (_artifacts.TryGetValue(cachedExitCode.CacheKey, out var entry)) {
            entry.LastAccessed = TimeUtils.Now();
        } else {
            _logger.Debug($"No cached artifact found for fingerprint {cachedExitCode.CacheKey} to update access time.");
        }
    }

    public async Task<ICreatedArtifact> AppendCachedDataAsync(ICreatedArtifact artifact, BuildConfig config, Project project) {
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
    public IBuildExitCode BuildFromDependencies(IArtifactBlueprint artifact, ICreatedArtifact createdArtifact, Project project, BuildConfig config, bool invalidateCache = false) {
        if (artifact is not IArtifactBuildable artifactBuildable) {
            throw new ConfigurationException($"Artifact '{artifact.Name}' is not buildable.");
        }

        var fingerprint = createdArtifact.GetFingerprint(project, config);
        var artifactRoot = GetArtifactRoot(config, project, createdArtifact);

        bool ExistsOnDisk() => Directory.Exists(artifactRoot);
        bool IsCached() => _artifacts.ContainsKey(fingerprint) && !invalidateCache;

        // Fast path: already built and recorded
        if (ExistsOnDisk() && IsCached()) {
            return new BuildExitCodeCached(fingerprint);
        }

        var gate = _buildLocks.GetOrAdd(fingerprint, _ => new SemaphoreSlim(1, 1));
        gate.Wait();
        try {
            // Re-check after acquiring the lock
            var exists = ExistsOnDisk();
            var cached = IsCached();
            if (exists && cached) return new BuildExitCodeCached(fingerprint);

            if (exists && !cached) Directory.Delete(artifactRoot, true);
            if (!Directory.Exists(artifactRoot)) _ = Directory.CreateDirectory(artifactRoot);

            if (artifact is IArtifactConsumable consumable) {
                _logger.Debug($"Consuming required dependencies for artifact {createdArtifact.Name}...");
                foreach (var dependency in createdArtifact.DependentArtifacts) {
                    var entry = GetRecentCachedArtifactForProject(dependency.Project);
                    consumable.Consume(dependency, entry.Output, dependency.Project);
                }
            }

            _logger.Debug($"Building artifact {createdArtifact.Name} with fingerprint {fingerprint} at {artifactRoot}");
            return artifactBuildable.Build(new(artifactRoot), project, config);
        } finally {
            _ = gate.Release();
            // Best-effort cleanup to avoid unbounded growth
            if (gate.CurrentCount == 1) {
                _ = _buildLocks.TryRemove(fingerprint, out _);
            }
        }
    }

    public ArtifactCacheEntry GetRecentCachedArtifactForProject(Project project) {
        var name = project.Name;

        List<Tuple<ArtifactCacheEntry, long>> candidates = new();
        foreach (var (key, entry) in _artifacts.OrderByDescending(kv => kv.Value.LastAccessed)) {
            if (key.StartsWith($"{name}-")) {
                candidates.Add(Tuple.Create(entry, entry.LastAccessed));
            }
        }

        return candidates.Count == 0
            ? throw new ManilaException($"No cached artifacts found for project '{name}'.")
            : candidates.OrderByDescending(t => t.Item2).First().Item1;
    }
}

public class ArtifactCacheEntry(string artifactRoot, string fingerprint, long createdAt, long lastAccessed, long size, LogCache logCache, ArtifactOutput output) {
    public string ArtifactRoot { get; set; } = artifactRoot;
    public string Fringerprint { get; } = fingerprint;
    public long CreatedAt { get; set; } = createdAt;
    public long LastAccessed { get; set; } = lastAccessed;
    public long Size { get; set; } = size;
    public LogCache LogCache { get; set; } = logCache;
    public ArtifactOutput Output { get; set; } = output;

    public static ArtifactCacheEntry FromArtifact(IArtifactManager artifactManager, ICreatedArtifact artifact, BuildConfig config, Project project, ArtifactOutput output) {
        return new(
            artifactManager.GetArtifactRoot(config, project, artifact),
            artifact.GetFingerprint(project, config),
            TimeUtils.Now(),
            TimeUtils.Now(),
            -1, // Size not implemented yet
            artifact.LogCache ?? throw new ManilaException(
                "Artifact does not have a log cache. Please ensure the artifact is properly executed before caching it."
            ),
            output
        );
    }
}
