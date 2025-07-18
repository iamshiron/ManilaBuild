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
        try {
            var zipConfig = (ZipBuildConfig) config;
            ManilaZip.Instance!.Debug($"Artifact Fingerprint: {artifact.GetFingerprint(config)} - SubFolder: {zipConfig.SubFolder}");

            foreach (var (key, set) in project.SourceSets) {
                var zipPath = Path.Join(ManilaEngine.GetInstance().ArtifactManager.GetArtifactRoot(config, project, artifact));
                var zipFile = Path.Join(zipPath, $"{key}.zip");

                ManilaEngine.GetInstance().ArtifactManager.CacheArtifact(artifact, config, project);

                if (!Directory.Exists(zipPath)) _ = Directory.CreateDirectory(zipPath);
                if (File.Exists(zipFile)) return new BuildExitCodeCached(zipFile);


                using var zip = ZipFile.Open(zipFile, ZipArchiveMode.Create);
                foreach (var file in set.Files) {
                    _ = zip.CreateEntryFromFile(
                        file,
                        zipConfig.SubFolder is not null ?
                            Path.Join(zipConfig.SubFolder, Path.GetRelativePath(set.Root, file)) :
                            Path.GetRelativePath(set.Root, file)
                    );
                }
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
