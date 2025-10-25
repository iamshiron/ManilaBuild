
using Shiron.Manila.API.Artifacts;
using Shiron.Manila.API.Exceptions;
using Shiron.Manila.API.Interfaces;
using Shiron.Manila.API.Interfaces.Artifacts;
using Shiron.Manila.Utils;

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
}
