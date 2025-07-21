
using Newtonsoft.Json;
using Shiron.Manila.Exceptions;

namespace Shiron.Manila.API;

/// <summary>
/// Represents an unresolved project. Used for the configuration of projects in build scripts, where the project is not yet resolved.
/// </summary>
public class UnresolvedProject(Workspace workspace, string identifier) {
    public readonly string Identifier = identifier;
    public readonly Workspace Workspace = workspace;

    /// <summary>
    /// Resolve the project from the identifier.
    /// </summary>
    /// <returns>The resolved project</returns>
    /// <exception cref="Exception">Project either does not exist or is unknown to the context</exception>
    public Project Resolve() {
        foreach (var pair in Workspace.Projects) {
            if (pair.Value.GetIdentifier() == Identifier) {
                return pair.Value;
            }
        }
        throw new ManilaException($"Project '{Identifier}' could not be resolved!");
    }

    /// <summary>
    /// Implicitly convert an unresolved project to a project. Used for convenience in build scripts.
    /// </summary>
    /// <param name="unresolved">The unresolved poject</param>
    public static implicit operator Project(UnresolvedProject unresolved) {
        return unresolved.Resolve();
    }
}
