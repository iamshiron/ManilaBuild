using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.ClearScript;
using Shiron.Manila.API.Artifacts;
using Shiron.Manila.API.Bridges;
using Shiron.Manila.API.Interfaces.Artifacts;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Utils;

namespace Shiron.Manila.API.Builders;

/// <summary>
/// A fluent builder for defining and creating an <see cref="Artifact"/>.
/// </summary>
public sealed class ArtifactBuilder(
    Workspace workspace,
    string baseComponent,
    ScriptObject configurator,
    Manila manilaAPI,
    Project project
) : IBuildable<IArtifact> {
    /// <summary>Gets the project this artifact belongs to.</summary>
    public readonly Project Project = project;

    /// <summary>Gets the user-defined description of the artifact.</summary>
    public string ArtifactDescription { get; private set; } = string.Empty;

    /// <summary>Gets the collection of job builders defined for this artifact.</summary>
    public readonly List<JobBuilder> JobBuilders = [];

    /// <summary>Gets the script function that configures this artifact.</summary>
    public readonly ScriptObject Lambda = configurator;

    /// <summary>Gets a reference to the top-level Manila API.</summary>
    public readonly Manila ManilaAPI = manilaAPI;

    /// <summary>Gets the matched plugin component this artifact is based on, if any.</summary>
    public RegexUtils.PluginComponentMatch PluginComponent =
        RegexUtils.MatchPluginComponent(baseComponent)
        ?? throw new ConfigurationException($"Invalid plugin component format: '{baseComponent}'. Expected 'plugin:component'.");

    /// <summary>Gets the name of the artifact.</summary>
    public string? Name { get; internal set; }

    private readonly Workspace _workspace = workspace;

    /// <summary>
    /// Sets the description for the artifact.
    /// </summary>
    /// <param name="description">A brief description of the artifact's purpose.</param>
    /// <returns>The current builder instance for chaining.</returns>
    public ArtifactBuilder Description(string description) {
        ArtifactDescription = description;
        return this;
    }

    /// <summary>
    /// Executes the configuration lambda to define jobs and constructs the final <see cref="Artifact"/>.
    /// </summary>
    /// <returns>The built <see cref="Artifact"/> instance.</returns>
    /// <exception cref="ConfigurationException">Thrown if the artifact name is not set.</exception>
    /// <exception cref="ScriptExecutionException">Thrown if an error occurs within the configuration script.</exception>
    /// <exception cref="BuildProcessException">Thrown for other unexpected errors during the build.</exception>
    [MemberNotNull(nameof(Name))]
    public IArtifact Build() {
        if (string.IsNullOrWhiteSpace(Name)) {
            throw new ConfigurationException("Artifact name must be set before building.");
        }

        ManilaAPI.CurrentArtifactBuilder = this;
        try {
            _ = Lambda.InvokeAsFunction(new UnresolvedArtifactScriptBridge(Project, Name, PluginComponent));
        } catch (ScriptEngineException se) {
            throw new ScriptExecutionException($"A script error occurred while configuring artifact '{Name}'.", se);
        } catch (Exception e) {
            throw new BuildProcessException($"An unexpected error occurred while building artifact '{Name}'.", e);
        } finally {
            ManilaAPI.CurrentArtifactBuilder = null;
        }

        return new Artifact(_workspace, this);
    }
}
