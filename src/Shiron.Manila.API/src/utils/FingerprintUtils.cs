
using System.Reflection;
using Shiron.Manila.API.Attributes;
using Shiron.Manila.API.Interfaces.Artifacts;
using Shiron.Profiling;
using Shiron.Utils;

namespace Shiron.Manila.API.Interfaces;

public static class FingerprintUtils {
    public static string HashConfigData(BuildConfig config, IProfiler? profiler = null) {
        ProfileScope? disposable = null;
        if (profiler is not null) disposable = new ProfileScope(profiler, MethodBase.GetCurrentMethod()!);

        List<string> hashes = [];
        foreach (var prop in config.GetType().GetProperties()) {
            if (Attribute.IsDefined(prop, typeof(FingerprintItem))) {
                var value = prop.GetValue(config)?.ToString() ?? string.Empty;
                hashes.Add(value);
            }
        }

        disposable?.Dispose();
        return HashUtils.CombineHashes(hashes);
    }

    public static string HashArtifact(ICreatedArtifact artifact, BuildConfig config, IProfiler? profiler = null) {
        ProfileScope? disposable = null;
        if (profiler is not null) disposable = new ProfileScope(profiler, MethodBase.GetCurrentMethod()!);

        var project = artifact.Project.Resolve();
        Dictionary<string, string> sourceSetFingerprints = project.SourceSets.ToDictionary(
            ss => ss.Key,
            ss => HashUtils.CreateFileSetHash(ss.Value.Files, ss.Value.Root)
        );

        var configFingerprint = HashConfigData(config);

        disposable?.Dispose();
        return HashUtils.CombineHashes([
            configFingerprint,
            sourceSetFingerprints.Count > 0 ? HashUtils.CombineHashes(sourceSetFingerprints.Values) : string.Empty
        ]);
    }
}
