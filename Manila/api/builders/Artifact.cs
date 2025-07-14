using Shiron.Manila.Artifacts;
using Shiron.Manila.Utils;

namespace Shiron.Manila.API.Builders;

/// <summary>
/// Builder for creating artifacts within a Manila build configuration.
/// </summary>
public sealed class ArtifactBuilder(Action lambda, Manila manilaAPI, BuildConfig buildConfig, string projectName) : IBuildable<Artifact> {
    /// <summary>
    /// The build configuration associated with this artifact.
    /// </summary>
    public readonly BuildConfig BuildConfig = buildConfig;

    /// <summary>
    /// The name of the project this artifact belongs to.
    /// </summary>
    public readonly string ProjectName = projectName;

    /// <summary>
    /// Description of the artifact.
    /// </summary>
    public string Description = string.Empty;

    /// <summary>
    /// Collection of task builders for this artifact.
    /// </summary>
    public readonly List<TaskBuilder> TaskBuilders = [];

    /// <summary>
    /// Lambda function that defines the artifact configuration.
    /// </summary>
    public readonly Action Lambda = lambda;

    /// <summary>
    /// Reference to the Manila API instance.
    /// </summary>
    public readonly Manila ManilaAPI = manilaAPI;

    /// <summary>
    /// The name of the artifact.
    /// </summary>
    public string? Name = null;

    /// <summary>
    /// Builds the artifact by executing the lambda configuration and creating an Artifact instance.
    /// </summary>
    /// <returns>The built artifact.</returns>
    public Artifact Build() {
        ManilaAPI.CurrentArtifactBuilder = this;
        Lambda.Invoke();
        ManilaAPI.CurrentArtifactBuilder = null;
        return new(this);
    }

    /// <summary>
    /// Sets the description for this artifact.
    /// </summary>
    /// <param name="description">The description text.</param>
    /// <returns>This builder instance for method chaining.</returns>
    public ArtifactBuilder description(string description) {
        Description = description;
        return this;
    }

    /// <summary>
    /// Configures dependencies for this artifact.
    /// </summary>
    /// <returns>This builder instance for method chaining.</returns>
    public ArtifactBuilder dependencies() {
        return this;
    }
}
