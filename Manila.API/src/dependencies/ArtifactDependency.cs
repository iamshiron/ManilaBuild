
using Shiron.Manila.API.Interfaces;

namespace Shiron.Manila.API.Dependencies;

public class ArtifactDependency(UnresolvedProject project, string artifact) : IDependency {
    private readonly UnresolvedProject _project = project;
    private readonly string _artifactName = artifact;
}
