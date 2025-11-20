
using Shiron.Manila.API.Artifacts;
using Shiron.Manila.API.Interfaces;
using Shiron.Manila.API.Interfaces.Artifacts;
using Shiron.Manila.API.Utils;
using Shiron.Manila.Exceptions;
using Shiron.Utils;

namespace Shiron.Manila.API.Dependencies;

public class ArtifactDependency(UnresolvedProject project, string artifact) : IDependency {
    private readonly UnresolvedProject _project = project;
    private readonly string _artifactName = artifact;

    public void Resolve(ICreatedArtifact artifact) {
        var project = _project.Resolve();
        var dependency = project.Artifacts[_artifactName];

        artifact.Jobs.First(j => j != null && j.Name == "build").Dependencies.Add(
            new RegexUtils.JobMatch(project.Name, _artifactName, "build").Format()
        );
        artifact.DependentArtifacts.Add(dependency);
    }

    public static IDependency Parse(object?[]? args) {
        return args == null || args.Length != 2
            ? throw new ManilaException("ArtifactDependency requires exactly 2 arguments: UnresolvedProject and artifact name.")
            : args[0] is not UnresolvedProject proj
            ? throw new ManilaException("First argument to ArtifactDependency must be of type UnresolvedProject.")
            : args[1] is not string artifactName
            ? throw new ManilaException("Second argument to ArtifactDependency must be a string representing the artifact name.")
            : (IDependency) new ArtifactDependency(proj, artifactName);
    }
}
