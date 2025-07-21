using System.Diagnostics.CodeAnalysis;
using Shiron.Manila.Artifacts;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Utils;

namespace Shiron.Manila.API.Builders;

/// <summary>
/// Builder for creating artifacts within a Manila build configuration.
/// </summary>
public sealed class ArtifactBuilder(Workspace workspace, Action lambda, Manila manilaAPI, BuildConfig buildConfig, string projectName) : IBuildable<Artifact> {
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
    /// Collection of job builders for this artifact.
    /// </summary>
    public readonly List<JobBuilder> JobBuilders = [];

    /// <summary>
    /// Lambda function that defines the artifact configuration.
    /// </summary>
    public readonly Action Lambda = lambda;

    /// <summary>
    /// Reference to the Manila API instance.
    /// </summary>
    public readonly Manila ManilaAPI = manilaAPI;

    /// <summary>
    /// Plugin component match for this artifact.
    /// </summary>
    public RegexUtils.PluginComponentMatch? PluginComponent;

    /// <summary>
    /// The name of the artifact.
    /// </summary>
    public string? Name = null;

    private readonly Workspace _workspace = workspace;

    /// <summary>
    /// Sets the description for this artifact.
    /// </summary>
    /// <param name="description">The description text.</param>
    /// <returns>This builder instance for method chaining.</returns>
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Exposed to JavaScript context")]
    public ArtifactBuilder description(string description) {
        Description = description;
        return this;
    }

    /// <summary>
    /// Applies plugin functionality to this artifact.
    /// </summary>
    /// <param name="plugin">The plugin to apply.</param>
    /// <returns>This builder instance for method chaining.</returns>
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Exposed to JavaScript context")]
    public ArtifactBuilder from(string key) {
        var temp = RegexUtils.MatchPluginComponent(key) ?? throw new ArgumentException($"Invalid plugin component format: {key}");
        PluginComponent = temp;

        return this;
    }

    /// <summary>
    /// Configures dependencies for this artifact.
    /// </summary>
    /// <returns>This builder instance for method chaining.</returns>
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Exposed to JavaScript context")]
    public ArtifactBuilder dependencies() {
        return this;
    }

    /// <summary>
    /// Builds the artifact by executing the lambda configuration and creating an Artifact instance.
    /// </summary>
    /// <returns>The built artifact.</returns>
    public Artifact Build() {
        ManilaAPI.CurrentArtifactBuilder = this;
        Lambda.Invoke();
        ManilaAPI.CurrentArtifactBuilder = null;
        return new(_workspace, this);
    }
}
