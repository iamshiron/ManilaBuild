using System.Diagnostics.CodeAnalysis;
using Microsoft.ClearScript;
using NuGet.Packaging;
using Shiron.Manila.API.Builders;
using Shiron.Manila.Artifacts;
using Shiron.Manila.Attributes;
using Shiron.Manila.Logging;
using Shiron.Manila.Utils;

namespace Shiron.Manila.API;

/// <summary>
/// Represents a project in the build script.
/// </summary>
public class Project(string name, string location, Workspace workspace) : Component(location) {
    /// <summary>
    /// The unique name of the project.
    /// </summary>
    [ScriptProperty(true)]
    public string Name { get; private set; } = name;

    /// <summary>
    /// The project version identifier.
    /// </summary>
    [ScriptProperty]
    public string? Version { get; set; }

    /// <summary>
    /// The project group identifier for organization.
    /// </summary>
    [ScriptProperty]
    public string? Group { get; set; }

    /// <summary>
    /// A brief description of the project.
    /// </summary>
    [ScriptProperty]
    public string? Description { get; set; }

    /// <summary>
    /// Collection of built artifacts produced by this project.
    /// </summary>
    public Dictionary<string, Artifact> Artifacts { get; } = [];

    /// <summary>
    /// Collection of source sets containing project source files.
    /// </summary>
    public Dictionary<string, SourceSet> SourceSets = [];

    /// <summary>
    /// Internal builders for creating artifacts during finalization.
    /// </summary>
    private readonly Dictionary<string, ArtifactBuilder> _artifactBuilders = [];

    /// <summary>
    /// Internal builders for creating source sets during finalization.
    /// </summary>
    private readonly Dictionary<string, SourceSetBuilder> _sourceSetBuilders = [];

    /// <summary>
    /// List of project dependencies.
    /// </summary>
    public readonly List<Dependency> Dependencies = [];

    /// <summary>
    /// The workspace containing this project.
    /// </summary>
    public Workspace Workspace { get; private set; } = workspace;

    /// <summary>
    /// Configures source sets for the project from a collection of builders.
    /// </summary>
    /// <param name="obj">Dictionary containing source set names and their builders.</param>
    [ScriptFunction]
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Exposed to JavaScript context")]
    public void sourceSets(object obj) {
        foreach (var pair in (IDictionary<string, object>) obj) {
            if (SourceSets.ContainsKey(pair.Key)) throw new Exception($"SourceSet '{pair.Key}' already exists.");
            _sourceSetBuilders.Add(pair.Key, (SourceSetBuilder) pair.Value);
        }
    }

    /// <summary>
    /// Adds dependencies to the project from a script object.
    /// </summary>
    /// <param name="obj">Script object containing dependency definitions.</param>
    [ScriptFunction]
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Exposed to JavaScript context")]
    public void dependencies(object obj) {
        ScriptObject sobj = (ScriptObject) obj;
        foreach (var n in sobj.PropertyIndices) {
            if (sobj[n] is Dependency dep) {
                Dependencies.Add(dep);
            } else {
                throw new InvalidCastException($"Property '{n}' is not a Dependency.");
            }
        }
    }

    /// <summary>
    /// Configures artifacts for the project from a collection of builders.
    /// </summary>
    /// <param name="obj">Dictionary containing artifact names and their builders.</param>
    [ScriptFunction]
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Exposed to JavaScript context")]
    public void artifacts(object obj) {
        foreach (var pair in (IDictionary<string, object>) obj) {
            if (_artifactBuilders.ContainsKey(pair.Key)) throw new Exception($"Artifact '{pair.Key}' already exists.");
            var builder = (ArtifactBuilder) pair.Value;
            builder.Name = pair.Key;
            _artifactBuilders[pair.Key] = builder;
        }
    }

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

        foreach (var (name, builder) in _artifactBuilders) {
            Artifacts[name] = builder.Build();
        }
        foreach (var (name, builder) in _sourceSetBuilders) {
            SourceSets[name] = builder.Build();
            Logger.Debug($"{Name} - {name} - SHA256: {SourceSets[name].Fingerprint()}");
        }
    }
}
