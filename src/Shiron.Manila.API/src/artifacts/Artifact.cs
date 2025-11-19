
using Shiron.Manila.API.Builders;
using Shiron.Manila.API.Interfaces;
using Shiron.Manila.API.Interfaces.Artifacts;
using Shiron.Manila.Exceptions;
using Shiron.Utils;

namespace Shiron.Manila.API.Artifacts;

/// <summary>
/// Represents a build artifact that implements the IArtifact interface.
/// </summary>
public class CreatedArtifact : ICreatedArtifact {
    public string Description { get; }
    public Job[] Jobs { get; }
    public string Name { get; }
    public UnresolvedProject Project { get; }
    public RegexUtils.PluginComponentMatch PluginComponent { get; }
    public IDependency[] Dependencies { get; } = [];
    public List<ICreatedArtifact> DependentArtifacts { get; } = [];
    public LogCache? LogCache { get; set; }
    public IArtifactBlueprint? ArtifactType { set; get; } = null;

    /// <summary>
    /// Initializes a new instance of the Artifact class using a builder pattern.
    /// </summary>
    /// <param name="workspace">The current workspace.</param>
    /// <param name="builder">The builder containing the artifact's configuration.</param>
    /// <exception cref="ManilaException">Thrown if essential properties like Name or PluginComponent are missing.</exception>
    public CreatedArtifact(Workspace workspace, ArtifactBuilder builder) {
        // Initialize properties from the builder.
        Description = builder.Description;
        Jobs = [.. builder.JobBuilders.Select(b => b.Build())];
        Dependencies = [.. builder.Dependencies];

        // Throw exceptions for invalid configurations.
        Name = builder.Name ?? throw new ManilaException($"Artifact must have a name! {builder.Description}");
        PluginComponent = builder.PluginComponent ?? throw new ManilaException($"Artifact must have a plugin component! {builder.Description}");

        // Initialize complex objects.
        Project = new UnresolvedProject(workspace, builder.Project.Name);

        // Initialize nullable properties.
        LogCache = null;
    }

    /// <summary>
    /// Returns a unique fingerprint for the artifact based on the project and build configuration.
    /// </summary>
    /// <param name="project">The project</param>
    /// <param name="config">The build configuration</param>
    /// <returns>A unique fingerprint</returns>
    public string GetFingerprint(Project project, BuildConfig config) => $"{project.Name}-{Name}_{FingerprintUtils.HashArtifact(this, config)}";
}
