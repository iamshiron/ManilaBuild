using System.IO.Compression;
using Shiron.Manila.API;
using Shiron.Manila.Artifacts;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Ext;

namespace Shiron.Manila.Zip.Components;

public class ZipComponent : LanguageComponent {
    public ZipComponent() : base("zip", typeof(ZipBuildConfig)) {
    }

    public override IBuildExitCode Build(Workspace workspace, Project project, BuildConfig config, Artifact artifact) {
        var instance = ManilaZip.Instance!;
        var artifactRoot = ManilaEngine.GetInstance().ArtifactManager.GetArtifactRoot(config, project, artifact);

        if (Directory.Exists(artifactRoot)) {
            return new BuildExitCodeCached(artifactRoot);
        }

        try {
            var zipConfig = (ZipBuildConfig) config;
            instance.Debug($"Artifact Fingerprint: {artifact.GetFingerprint(config)} - SubFolder: {zipConfig.SubFolder}");

            instance.Info($"Building zip artifact for project {project.Name} with artifact {artifact.Name}");
            foreach (var (key, set) in project.SourceSets) {
                instance.Debug($"Processing source set '{key}' with root '{set.Root}' and {set.Files.Length} files");
                var zipFile = Path.Join(artifactRoot, $"{key}.zip");

                _ = Directory.CreateDirectory(artifactRoot);


                using var zip = ZipFile.Open(zipFile, ZipArchiveMode.Create);
                foreach (var file in set.Files) {
                    _ = zip.CreateEntryFromFile(
                        file,
                        zipConfig.SubFolder is not null ?
                            Path.Join(zipConfig.SubFolder, Path.GetRelativePath(set.Root, file)) :
                            Path.GetRelativePath(set.Root, file)
                    );
                }

                instance.Info($"Created zip artifact at {zipFile} with {set.Files.Length} files.");
            }

            return new BuildExitCodeSuccess();
        } catch (Exception ex) {
            var e = new ManilaException($"Failed to build zip artifact: {ex.Message}");
            return new BuildExitCodeFailed(e);
        }
    }

    public override void Run(Project project) {
        throw new ManilaException("Cannot run a zip project.");
    }
}
