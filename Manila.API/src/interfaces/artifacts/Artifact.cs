
using Shiron.Manila.API.Builders;
using Shiron.Manila.API.Utils;
using Shiron.Manila.Interfaces;
using Shiron.Manila.Utils;

namespace Shiron.Manila.API.Interfaces.Artifacts;

public interface ICreatedArtifact {
    string Description { get; }
    Job[] Jobs { get; }
    string Name { get; }
    UnresolvedProject Project { get; }
    RegexUtils.PluginComponentMatch PluginComponent { get; }
    IDependency[] Dependencies { get; }
    List<ICreatedArtifact> DependentArtifacts { get; }
    IArtifactBlueprint? ArtifactType { set; get; }

    LogCache? LogCache { get; set; }

    string GetFingerprint(Project project, BuildConfig config);
}

public record ArtifactOutput(string ArtifactRoot, string[] FilePaths);

public sealed class ArtifactOutputBuilder(string root) {
    private readonly string _root = root;
    private readonly List<string> _filePaths = [];
    private readonly List<SubArtifactOutputBuilder> _subArtifacts = [];

    public class SubArtifactOutputBuilder {
        public readonly string RootInArtifact;
        public readonly List<string> FilePaths = [];

        internal SubArtifactOutputBuilder(string rootInArtifact) {
            RootInArtifact = rootInArtifact;
        }

        public SubArtifactOutputBuilder AddFile(string filePath) {
            FilePaths.Add(filePath);
            return this;
        }

        public string GetPathInArtifact(string relativePath) {
            return Path.Join(RootInArtifact, relativePath);
        }
    }

    public SubArtifactOutputBuilder CreateSubArtifact(string? root = null) {
        var subArtifact = new SubArtifactOutputBuilder(root ?? ".");
        _subArtifacts.Add(subArtifact);
        return subArtifact;
    }

    public ArtifactOutput Build() {
        var allFilePaths = _filePaths
            .Concat(_subArtifacts.SelectMany(subArtifact =>
                subArtifact.FilePaths.Select(file =>
                    (subArtifact.RootInArtifact == "." ? "" : subArtifact.RootInArtifact + "/") + file)))
            .ToArray();

        return new ArtifactOutput(_root, allFilePaths);
    }

    public string GetPathInArtifact(string relativePath) {
        return Path.Join(_root, relativePath);
    }
}

public interface IArtifactBlueprint {
    Type BuildConfigType { get; }
}
public interface IArtifactBuildable : IArtifactBlueprint {
    IBuildExitCode Build(ArtifactOutputBuilder builder, Project project, BuildConfig config);
}
public interface IArtifactConsumable<T> : IArtifactBlueprint where T : IArtifactBlueprint {
    void Consume(ICreatedArtifact artifact, ArtifactOutput output, Project project, T artifactType);
}
public interface IArtifactExecutable : IArtifactBlueprint { }
public interface IArtifactTransientExecutable : IArtifactBlueprint { }
