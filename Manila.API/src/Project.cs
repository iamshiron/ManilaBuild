using Newtonsoft.Json;
using Shiron.Manila.API.Builders;
using Shiron.Manila.API.Interfaces.Artifacts;
using Shiron.Manila.Logging;
using Shiron.Manila.Utils;

namespace Shiron.Manila.API;

/// <summary>
/// Represents a project in the build script.
/// </summary>
public class Project(ILogger logger, string name, string projectRoot, string root, Workspace workspace) : Component(logger, root, projectRoot) {
    /// <summary>
    /// The unique name of the project.
    /// </summary>
    public string Name { get; private set; } = name;

    /// <summary>
    /// The project version identifier.
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// The project group identifier for organization.
    /// </summary>
    public string? Group { get; set; }

    /// <summary>
    /// A brief description of the project.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Collection of built artifacts produced by this project.
    /// </summary>
    public Dictionary<string, IArtifact> Artifacts { get; } = [];

    /// <summary>
    /// Collection of source sets containing project source files.
    /// </summary>
    public Dictionary<string, SourceSet> SourceSets = [];

    /// <summary>
    /// Internal builders for creating artifacts during finalization.
    /// </summary>
    public readonly Dictionary<string, ArtifactBuilder> ArtifactBuilders = [];

    /// <summary>
    /// Internal builders for creating source sets during finalization.
    /// </summary>
    public readonly Dictionary<string, SourceSetBuilder> SourceSetBuilders = [];

    /// <summary>
    /// The workspace containing this project.
    /// </summary>
    public Workspace Workspace { get; private set; } = workspace;
    private readonly ILogger _logger = logger;

    /// <summary>
    /// Returns a string representation of the project.
    /// </summary>
    public override string ToString() {
        return $"Project({GetIdentifier()})";
    }

    /// <summary>
    /// Finalizes the project by building all artifacts and source sets.
    /// </summary>
    /// <param name="manilaAPI">The Manila API instance for finalization.</param>
    public override void Finalize(Manila manilaAPI) {
        base.Finalize(manilaAPI);

        foreach (var (name, builder) in ArtifactBuilders) {
            Artifacts[name] = builder.Build();
        }
        foreach (var (name, builder) in SourceSetBuilders) {
            SourceSets[name] = builder.Build();
            _logger.Debug($"{Name} - {name} - SHA256: {SourceSets[name].Fingerprint()}");
        }
    }
}
