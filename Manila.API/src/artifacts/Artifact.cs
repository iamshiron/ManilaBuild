
using Shiron.Manila.API.Builders;
using Shiron.Manila.API.Interfaces;
using Shiron.Manila.API.Interfaces.Artifacts;
using Shiron.Manila.API.Utils;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Utils;

namespace Shiron.Manila.API.Artifacts;

/// <summary>
/// Represents a build artifact that implements the IArtifact interface.
/// </summary>
public class Artifact : ICreatedArtifact {
    public string Description { get; }
    public Job[] Jobs { get; }
    public string Name { get; }
    public UnresolvedProject Project { get; }
    public RegexUtils.PluginComponentMatch PluginComponent { get; }

    public LogCache? LogCache { get; set; }

    /// <summary>
    /// Initializes a new instance of the Artifact class using a builder pattern.
    /// </summary>
    /// <param name="workspace">The current workspace.</param>
    /// <param name="builder">The builder containing the artifact's configuration.</param>
    /// <exception cref="ManilaException">Thrown if essential properties like Name or PluginComponent are missing.</exception>
    public Artifact(Workspace workspace, ArtifactBuilder builder) {
        // Initialize properties from the builder.
        Description = builder.ArtifactDescription;
        Jobs = builder.JobBuilders.Select(b => b.Build()).ToArray();

        // Throw exceptions for invalid configurations.
        Name = builder.Name ?? throw new ManilaException($"Artifact must have a name! {builder.ArtifactDescription}");
        PluginComponent = builder.PluginComponent ?? throw new ManilaException($"Artifact must have a plugin component! {builder.Description}");

        // Initialize complex objects.
        Project = new UnresolvedProject(workspace, builder.Project.Name);

        // Initialize nullable properties.
        LogCache = null;
    }

    public string GetFingerprint(Project project, BuildConfig config) => $"{project.Name}-{Name}_{FingerprintUtils.HashArtifact(this, config)}";
}
