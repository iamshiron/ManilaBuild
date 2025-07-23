using System.Diagnostics.CodeAnalysis;
using Microsoft.ClearScript;
using NuGet.Common;
using Shiron.Manila.API.Bridges;
using Shiron.Manila.Artifacts;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Utils;

namespace Shiron.Manila.API.Builders;

/// <summary>
/// Builder for creating artifacts within a Manila build configuration.
/// </summary>
public sealed class ArtifactBuilder(Workspace workspace, ScriptObject configurator, Manila manilaAPI, BuildConfig buildConfig, Project project) : IBuildable<Artifact> {
    /// <summary>
    /// The build configuration associated with this artifact.
    /// </summary>
    public readonly BuildConfig BuildConfig = buildConfig;

    /// <summary>
    /// The name of the project this artifact belongs to.
    /// </summary>
    public readonly Project Project = project;

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
    public readonly ScriptObject Lambda = configurator;

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
        if (Name == null) throw new ManilaException("Artifact name must be set before building.");

        ManilaAPI.CurrentArtifactBuilder = this;
        try {
            _ = Lambda.InvokeAsFunction(new UnresolvedArtifactScriptBridge(
                Project, Name
            ));
        } catch (Exception ex) {
            throw new ManilaException($"Error while building artifact '{Name}': {ex.Message}", ex);
        }

        ManilaAPI.CurrentArtifactBuilder = null;
        return new(_workspace, this);
    }
}
