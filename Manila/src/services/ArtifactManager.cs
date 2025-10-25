
using System.Collections.Concurrent;
using Shiron.Manila.API;
using Shiron.Manila.API.Exceptions;
using Shiron.Manila.API.Interfaces;
using Shiron.Manila.API.Interfaces.Artifacts;
using Shiron.Manila.API.Utils;
using Shiron.Manila.Interfaces;
using Shiron.Manila.Logging;
using Shiron.Manila.Profiling;
using Shiron.Manila.Utils;

namespace Shiron.Manila.Services;

public class ArtifactManager(ILogger logger, IProfiler profiler, string artifactsDir, IArtifactCache cache) : IArtifactManager {
    private readonly ILogger _logger = logger;
    private readonly IProfiler _profiler = profiler;
    private readonly IArtifactCache _cache = cache;

    public readonly string ArtifactsDir = artifactsDir;
    // Prevents concurrent duplicate builds of the same artifact fingerprint
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _buildLocks = new();

    public string GetArtifactRoot(BuildConfig config, Project project, ICreatedArtifact artifact) {
        return Path.Join(
            ArtifactsDir,
            $"{PlatformUtils.GetPlatformKey()}-{PlatformUtils.GetArchitectureKey()}",
            $"{project.Name}-{artifact.Name}",
            artifact.GetFingerprint(project, config),
            config.GetArtifactKey()
        );
    }

    public IBuildExitCode BuildFromDependencies(IArtifactBlueprint artifact, ICreatedArtifact createdArtifact, Project project, BuildConfig config, bool invalidateCache = false) {
        if (artifact is not IArtifactBuildable artifactBuildable) {
            throw new ConfigurationException($"Artifact '{artifact.GetType().FullName}' is not buildable.");
        }

        createdArtifact.ArtifactType = artifact;
        var fingerprint = createdArtifact.GetFingerprint(project, config);
        var artifactRoot = GetArtifactRoot(config, project, createdArtifact);

        bool ExistsOnDisk() => Directory.Exists(artifactRoot);
        bool IsCached() => _cache.IsCached(fingerprint) && !invalidateCache;

        // Fast path: already built and recorded
        if (ExistsOnDisk() && IsCached()) {
            return new BuildExitCodeCached(fingerprint);
        }

        var gate = _buildLocks.GetOrAdd(fingerprint, _ => new SemaphoreSlim(1, 1));
        gate.Wait();
        try {
            var exists = ExistsOnDisk();
            var cached = IsCached();
            if (exists && cached) return new BuildExitCodeCached(fingerprint);

            if (exists && !cached) Directory.Delete(artifactRoot, true);
            if (!Directory.Exists(artifactRoot)) _ = Directory.CreateDirectory(artifactRoot);

            foreach (var dependency in createdArtifact.DependentArtifacts) {
                if (dependency.ArtifactType == null)
                    throw new ConfigurationException($"Artifact '{dependency.Name}' does not have an associated ArtifactType. Usually this indicates a artifact was not built properly before being added as a dependency.");

                var blueprintType = createdArtifact.ArtifactType.GetType();
                var consumableType = typeof(IArtifactConsumable<>).MakeGenericType(blueprintType);
                if (!consumableType.IsAssignableFrom(artifact.GetType()))
                    throw new ConfigurationException($"Artifact '{artifact.GetType().FullName}' does not implement IArtifactConsumable<{blueprintType.FullName}> required to consume dependency '{dependency.Name}'.");

                _logger.Debug($"Consuming required dependencies for artifact {createdArtifact.Name}...");
                _logger.Debug($"Consuming dependency artifact {dependency.Name} for project {dependency.Project.Resolve().Name}...");

                var output = _cache.GetMostRecentOutputForProject(dependency.Project);
                var consumeMethod = consumableType.GetMethod("Consume");
                _ = consumeMethod?.Invoke(artifact, [dependency, output, dependency.Project.Resolve(), artifact]);
            }

            _logger.Debug($"Building artifact {createdArtifact.Name} with fingerprint {fingerprint} at {artifactRoot}");
            return artifactBuildable.Build(new(artifactRoot), project, config);
        } finally {
            _ = gate.Release();
            if (gate.CurrentCount == 1) {
                _ = _buildLocks.TryRemove(fingerprint, out _);
            }
        }
    }

}
