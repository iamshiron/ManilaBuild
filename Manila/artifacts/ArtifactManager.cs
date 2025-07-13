
using Shiron.Manila.API;
using Shiron.Manila.Utils;

namespace Shiron.Manila.Artifacts;

public class ArtifactManager(string artifactsDir) {
    public readonly string ArtifactsDir = artifactsDir;

    public string GetArtifactRoot(BuildConfig config, string projectName, string artifactName) {
        return Path.Join(
            ArtifactsDir,
            $"{PlatformUtils.GetPlatformKey()}-{PlatformUtils.GetArchitectureKey()}",
            $"{projectName}-{artifactName}",
            config.GetArtifactKey()
        );
    }
}
