using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.ClearScript;
using Shiron.Manila.API.Bridges;
using Shiron.Manila.Artifacts;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Utils;

namespace Shiron.Manila.API.Builders;

/// <summary>
/// A fluent builder for defining and creating an <see cref="Artifact"/>.
/// </summary>
public sealed class ArtifactBuilder(
    Workspace workspace,
    ScriptObject configurator,
    Manila manilaAPI,
    BuildConfig buildConfig,
    Project project
) : IBuildable<Artifact> {
    /// <summary>Gets the build configuration this artifact is a part of.</summary>
    public readonly BuildConfig BuildConfig = buildConfig;

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
    public RegexUtils.PluginComponentMatch? PluginComponent { get; private set; }

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
    /// Specifies a plugin component as the source for this artifact's jobs.
    /// </summary>
    /// <param name="key">The plugin component identifier (e.g., "plugin-name:component-name").</param>
    /// <returns>The current builder instance for chaining.</returns>
    /// <exception cref="ConfigurationException">Thrown if the key format is invalid.</exception>
    public ArtifactBuilder From(string key) {
        PluginComponent = RegexUtils.MatchPluginComponent(key)
            ?? throw new ConfigurationException($"Invalid plugin component format: '{key}'. Expected 'plugin:component'.");

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
    public Artifact Build() {
        if (string.IsNullOrWhiteSpace(Name)) {
            throw new ConfigurationException("Artifact name must be set before building.");
        }

        ManilaAPI.CurrentArtifactBuilder = this;
        try {
            _ = Lambda.InvokeAsFunction(new UnresolvedArtifactScriptBridge(Project, Name));
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
