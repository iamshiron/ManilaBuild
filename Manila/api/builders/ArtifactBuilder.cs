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
    public string ArtifactDescription = string.Empty;

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

    public ArtifactBuilder Description(string description) {
        ArtifactDescription = description;
        return this;
    }
    public ArtifactBuilder From(string key) {
        var temp = RegexUtils.MatchPluginComponent(key) ?? throw new ArgumentException($"Invalid plugin component format: {key}");
        PluginComponent = temp;

        return this;
    }
    public ArtifactBuilder Dependencies() {
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
