
using Shiron.Manila.API;
using Shiron.Manila.API.Dependencies;
using Shiron.Manila.API.Interfaces;
using Shiron.Manila.API.Interfaces.Artifacts;

namespace Shiron.Manila.JS;

public class NPMDepndency(string package, string? version) : IDependency {
    private readonly string _packageName = package;
    private readonly string _version = version ?? "*";

    void IDependency.Resolve(ICreatedArtifact artifact) {
        ManilaJS.Instance!.Debug($"Adding NPM dependency: {_packageName}@{_version}");
    }
}
