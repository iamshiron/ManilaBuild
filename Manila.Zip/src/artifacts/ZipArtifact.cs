using System.Diagnostics.Contracts;
using System.IO.Compression;
using Shiron.Manila.API;
using Shiron.Manila.API.Attributes;
using Shiron.Manila.API.Builders;
using Shiron.Manila.API.Interfaces;
using Shiron.Manila.API.Interfaces.Artifacts;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Interfaces;

namespace Shiron.Manila.Zip.Artifacts;

[ManilaExpose]
public class ZipArtifact : IArtifactBuildable, IArtifactConsumable {
    public string Name => "zip";
    public Type BuildConfigType => typeof(ZipBuildConfig);

    private readonly List<ArtifactOutput> _dependencies = [];

    public IBuildExitCode Build(ArtifactOutputBuilder builder, Project project, BuildConfig config) {
        var instance = ManilaZip.Instance!;

        var rootBuilder = builder.CreateSubArtifact();

        try {
            var zipConfig = (ZipBuildConfig) config;
            instance.Debug("Creating Zip Artifact...");

            foreach (var (key, set) in project.SourceSets) {
                instance.Debug($"Processing source set '{key}' with root '{set.Root}' and {set.Files.Length} files");
                var zipFile = builder.GetPathInArtifact($"{key}.zip");
                _ = rootBuilder.AddFile(zipFile);

                using var zip = ZipFile.Open(zipFile, ZipArchiveMode.Create);
                foreach (var file in set.Files) {
                    _ = zip.CreateEntryFromFile(
                        file,
                        zipConfig.SubFolder is not null ?
                            Path.Join(zipConfig.SubFolder, Path.GetRelativePath(set.Root, file)) :
                            Path.GetRelativePath(set.Root, file)
                    );
                }

                foreach (var d in _dependencies) {
                    foreach (var f in d.FilePaths) {
                        _ = zip.CreateEntryFromFile(
                            f,
                            zipConfig.SubFolder is not null ?
                                Path.Join(zipConfig.SubFolder, f) :
                                Path.Join(d.ArtifactRoot, f)
                        );
                    }
                }

                instance.Info($"Created zip artifact at {zipFile} with {set.Files.Length} files.");
            }

            return new BuildExitCodeSuccess(builder);
        } catch (Exception ex) {
            var e = new ManilaException($"Failed to build zip artifact: {ex.Message}");
            return new BuildExitCodeFailed(e);
        }
    }

    public void Consume(IArtifactBlueprint artifact, ArtifactOutput output, Project project) {
        _dependencies.Add(output);
    }
}
