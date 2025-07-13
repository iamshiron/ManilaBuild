
using Shiron.Manila.API;

namespace Shiron.Manila.Artifacts;

public class ArtifactManager(string artifactsDir) {
    public readonly string ArtifactsDir = artifactsDir;

    public string GetArtifactRoot(BuildConfig config, string propjectName, string artifactName) {
        return Path.Join(
            ArtifactsDir,
            config.platform,
            $"{propjectName}-{artifactName}_{config.config}-{config.architecture}"
        );
    }
}
