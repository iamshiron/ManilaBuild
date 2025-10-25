
using Shiron.Manila.API.Interfaces.Artifacts;
using Shiron.Manila.Interfaces;

namespace Shiron.Manila.API.Interfaces;

public interface IArtifactManager {
    string GetArtifactRoot(BuildConfig config, Project project, ICreatedArtifact artifact);
    IBuildExitCode BuildFromDependencies(IArtifactBlueprint artifact, ICreatedArtifact createdArtifact, Project project, BuildConfig config, bool invalidateCache);
}
