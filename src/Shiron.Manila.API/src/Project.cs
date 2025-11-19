using Newtonsoft.Json;
using Shiron.Logging;
using Shiron.Manila.API.Builders;
using Shiron.Manila.API.Interfaces.Artifacts;
using Shiron.Utils;

namespace Shiron.Manila.API;

/// <summary>Project definition; groups source sets, artifacts, jobs.</summary>
/// <param name="logger">Logger instance.</param>
/// <param name="name">Project name (unique).</param>
/// <param name="projectRoot">Project root directory.</param>
/// <param name="root">Workspace root directory.</param>
/// <param name="workspace">Owning workspace.</param>
public class Project(ILogger logger, string name, string projectRoot, string root, Workspace workspace) : Component(logger, root, projectRoot) {
    /// <summary>Unique project name.</summary>
    public string Name { get; private set; } = name;

    /// <summary>Version identifier.</summary>
    public string? Version { get; set; }

    /// <summary>Group identifier.</summary>
    public string? Group { get; set; }

    /// <summary>Short description.</summary>
    public string? Description { get; set; }

    /// <summary>Built artifacts keyed by name.</summary>
    public Dictionary<string, ICreatedArtifact> Artifacts { get; } = [];

    /// <summary>Source sets keyed by ID.</summary>
    public Dictionary<string, SourceSet> SourceSets = [];

    /// <summary>Artifact builders (internal build pipeline).</summary>
    public readonly Dictionary<string, ArtifactBuilder> ArtifactBuilders = [];

    /// <summary>Source set builders (internal build pipeline).</summary>
    public readonly Dictionary<string, SourceSetBuilder> SourceSetBuilders = [];

    /// <summary>Owning workspace.</summary>
    public Workspace Workspace { get; private set; } = workspace;
    private readonly ILogger _logger = logger;

    /// <summary>Debug string.</summary>
    public override string ToString() {
        return $"Project({GetIdentifier()})";
    }

    /// <summary>Build artifacts and source sets.</summary>
    /// <param name="manilaAPI">Manila API context.</param>
    public override void Finalize(Manila manilaAPI) {
        base.Finalize(manilaAPI);

        foreach (var (name, builder) in ArtifactBuilders) Artifacts[name] = builder.Build();
        foreach (var (name, builder) in SourceSetBuilders) {
            SourceSets[name] = builder.Build();
            _logger.Debug($"{Name} - {name} - SHA256: {SourceSets[name].Fingerprint()}");
        }
    }
}
