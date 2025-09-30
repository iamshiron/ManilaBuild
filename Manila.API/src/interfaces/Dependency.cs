
using Shiron.Manila.API.Artifacts;
using Shiron.Manila.API.Interfaces.Artifacts;

namespace Shiron.Manila.API.Interfaces;

public interface IDependency {
    void Resolve(ICreatedArtifact artifact);
}
