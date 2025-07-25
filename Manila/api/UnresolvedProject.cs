using System.Linq;
using Newtonsoft.Json;
using Shiron.Manila.Exceptions;

namespace Shiron.Manila.API;

/// <summary>
/// Represents a lightweight, lazy-loading proxy for a <see cref="Project"/>.
/// It's used in build scripts to reference other projects by their identifiers
/// before they are all fully loaded and resolved.
/// </summary>
public class UnresolvedProject(Workspace workspace, string identifier) {
    /// <summary>
    /// Gets the unique identifier of the project to be resolved.
    /// </summary>
    public readonly string Identifier = identifier;

    /// <summary>
    /// Gets the workspace context where the project is expected to exist.
    /// This property is ignored during JSON serialization to prevent circular references.
    /// </summary>
    [JsonIgnore]
    public readonly Workspace Workspace = workspace;

    /// <summary>
    /// Resolves the project reference from the identifier within the workspace.
    /// </summary>
    /// <returns>The resolved <see cref="Project"/> instance.</returns>
    /// <exception cref="ConfigurationException">
    /// Thrown if a project with the specified identifier does not exist in the workspace.
    /// </exception>
    public Project Resolve() {
        var project = Workspace.Projects.Values.FirstOrDefault(p => p.GetIdentifier() == Identifier);

        return project ?? throw new ConfigurationException(
            $"The project '{Identifier}' could not be resolved. Ensure it is defined in the workspace and the identifier is correct."
        );
    }

    /// <summary>
    /// Implicitly converts an unresolved project to a project by invoking <see cref="Resolve"/>.
    /// This provides convenience for users in build scripts.
    /// </summary>
    /// <param name="unresolved">The unresolved project to convert.</param>
    public static implicit operator Project(UnresolvedProject unresolved) {
        return unresolved.Resolve();
    }
}
