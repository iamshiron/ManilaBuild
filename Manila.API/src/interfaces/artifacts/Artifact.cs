
using Shiron.Manila.API.Utils;
using Shiron.Manila.Interfaces;
using Shiron.Manila.Utils;

namespace Shiron.Manila.API.Interfaces.Artifacts;

public interface IArtifact {
    string Description { get; }
    Job[] Jobs { get; }
    string Name { get; }
    UnresolvedProject Project { get; }
    RegexUtils.PluginComponentMatch PluginComponent { get; }

    LogCache? LogCache { get; set; }

    string GetFingerprint(BuildConfig config);
}

public static class ArtifactTypes {
    public static readonly int STATIC_LIB = 1;
    public static readonly int DYNAMIC_LIB = 2;
    public static readonly int EXECUTABLE = 4;
    public static readonly int ARCHIVE = 8;
    public static readonly int RUNTIME = 16;
}

public interface IArtifactBuilder {
    string Name { get; }
    Type BuildConfigType { get; }
    IBuildExitCode Build(string artifactRoot, Project project, BuildConfig config, IArtifact artifact, IArtifactOutput[] dependencies);
}

public interface IArtifactOutput {
    string[] FilePaths { get; }
    long SizeInBytes { get; }

    int ArtifactType { get; }
}
public interface IConditionalArtifactOutput {
    bool Predicate();
}
