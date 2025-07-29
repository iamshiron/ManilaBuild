using System.IO.Compression;
using Shiron.Manila.API;
using Shiron.Manila.API.Attributes;
using Shiron.Manila.API.Interfaces;
using Shiron.Manila.API.Interfaces.Artifacts;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Interfaces;

namespace Shiron.Manila.Zip.Artifacts;

[ManilaExpose]
public class ZipArtifactOutput(string[] files) : IArtifactOutput {
    private readonly string[] _files = files;
    private readonly long _size = files.Sum(f => new FileInfo(f).Length);

    public string[] FilePaths => _files;
    public long SizeInBytes => _size;
    public int ArtifactType => ArtifactTypes.ARCHIVE;
}

public class ZipArtifact : IArtifactBuilder {
    public string Name => "zip";
    public Type BuildConfigType => typeof(ZipBuildConfig);

    public IBuildExitCode Build(string artifactRoot, Project project, BuildConfig config, IArtifact artifact, IArtifactOutput[] dependencies) {
        var instance = ManilaZip.Instance!;

        try {
            var zipConfig = (ZipBuildConfig) config;
            instance.Debug($"Artifact Fingerprint: {artifact.GetFingerprint(config)} - SubFolder: {zipConfig.SubFolder}");

            instance.Info($"Building zip artifact for project {project.Name} with artifact {artifact.Name}");

            List<string> files = [];
            foreach (var (key, set) in project.SourceSets) {
                instance.Debug($"Processing source set '{key}' with root '{set.Root}' and {set.Files.Length} files");
                var zipFile = Path.Join(artifactRoot, $"{key}.zip");
                files.Add(zipFile);

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

                foreach (var d in dependencies) {
                    foreach (var f in d.FilePaths) {
                        _ = zip.CreateEntryFromFile(
                            f,
                            zipConfig.SubFolder is not null ?
                                Path.Join(zipConfig.SubFolder, f) :
                                f
                        );
                    }
                }

                instance.Info($"Created zip artifact at {zipFile} with {set.Files.Length} files.");
            }

            return new BuildExitCodeSuccess([
                new ZipArtifactOutput([..files])
            ]);
        } catch (Exception ex) {
            var e = new ManilaException($"Failed to build zip artifact: {ex.Message}");
            return new BuildExitCodeFailed(e);
        }
    }
}
