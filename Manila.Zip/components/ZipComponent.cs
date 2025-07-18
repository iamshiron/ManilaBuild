using System.IO.Compression;
using Shiron.Manila.API;
using Shiron.Manila.Artifacts;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Ext;

namespace Shiron.Manila.Zip.Components;

public class ZipComponent : LanguageComponent {
    public ZipComponent() : base("zip", typeof(ZipBuildConfig)) {
    }

    public override void Build(Workspace workspace, Project project, BuildConfig config, Artifact artifact) {
        var zipConfig = (ZipBuildConfig) config;

        ManilaZip.Instance!.Debug($"Artifact Fingerprint: {artifact.GetFingerprint(config)}");

        foreach (var (key, set) in project.SourceSets) {
            ManilaZip.Instance!.Debug($"Building source set '{key}' for project '{project.Name}' using zip component.");
            ManilaZip.Instance!.Debug($"Files: {string.Join(", ", set.Files)}");

            var zipPath = Path.Join(ManilaEngine.GetInstance().ArtifactManager.GetArtifactRoot(config, project.Name, artifact.Name));
            var zipFile = Path.Join(zipPath, $"{key}.zip");

            if (!Directory.Exists(zipPath)) _ = Directory.CreateDirectory(zipPath);
            if (File.Exists(zipFile)) File.Delete(zipFile); // Regenerate always for now til proper incremental build is implemented

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
    }

    public override void Run(Project project) {
        throw new ManilaException("Cannot run a zip project.");
    }
}
