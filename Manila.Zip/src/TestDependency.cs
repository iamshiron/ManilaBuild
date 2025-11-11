using Shiron.Manila.API.Interfaces;
using Shiron.Manila.API.Interfaces.Artifacts;

namespace Shiron.Manila.Zip;

public class TestDependency(string package, string version, string? artifact) : IDependency {
    public readonly string Package = package;
    public readonly string Version = version;
    public readonly string? Artifact = artifact;

    public void Resolve(ICreatedArtifact artifact) {
        ManilaZip.Instance?.Info($"Resolving TestDependency: Package='{Package}', Version='{Version}', Artifact='{Artifact}'");
    }
    public static IDependency Parse(object?[]? args) {
        if (args == null) throw new ArgumentException("Arguments cannot be null");
        if (args.Length < 3) throw new ArgumentException("At least 3 arguments are required");
        return new TestDependency(
            args[0] is string pkg ? pkg : throw new ArgumentException("First argument must be a string"),
            args[1] is string ver ? ver : throw new ArgumentException("Second argument must be a string"),
            args[2] is string art ? art : null
        );
    }
}
