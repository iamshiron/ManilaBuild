
using Newtonsoft.Json;
using Shiron.Manila.API;
using Shiron.Manila.Logging;
using Shiron.Manila.Utils;

namespace Shiron.Manila.Artifacts;

public class ArtifactManager(string artifactsDir, string artifactsCacheFile) {
    public readonly string ArtifactsDir = artifactsDir;
    public readonly string ArtifactsCacheFile = artifactsCacheFile;

    private static readonly JsonSerializerSettings _jsonSettings = new() {
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Include,
        DefaultValueHandling = DefaultValueHandling.Include,
        TypeNameHandling = TypeNameHandling.Objects,
        TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
    };

    private readonly Dictionary<string, Artifact> _artifacts = [];

    public string GetArtifactRoot(BuildConfig config, Project project, Artifact artifact) {
        return Path.Join(
            ArtifactsDir,
            $"{PlatformUtils.GetPlatformKey()}-{PlatformUtils.GetArchitectureKey()}",
            $"{project.Name}-{artifact.Name}",
            artifact.GetFingerprint(config),
            config.GetArtifactKey()
        );
    }

    public void CacheArtifact(Artifact artifact, BuildConfig config, Project project) {
        _artifacts[GetArtifactRoot(config, project, artifact)] = artifact;
    }

    public void FlushCacheToDisk() {
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
}
