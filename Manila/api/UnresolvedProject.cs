namespace Shiron.Manila.API;

/// <summary>
/// Represents an unresolved project. Used for the configuration of projects in build scripts, where the project is not yet resolved.
/// </summary>
public class UnresolvedProject {
    public readonly string identifier;

    public UnresolvedProject(string identifier) { this.identifier = identifier; }

    /// <summary>
    /// Resolve the project from the identifier.
    /// </summary>
    /// <returns>The resolved project</returns>
    /// <exception cref="Exception">Project either does not exist or is unknown to the context</exception>
    public Project Resolve() {
        foreach (var pair in ManilaEngine.GetInstance().Workspace.Projects) {
            if (pair.Value.GetIdentifier() == identifier) {
                return pair.Value;
            }
        }
        throw new Exception("Project not found: " + identifier);
    }

    /// <summary>
    /// Implicitly convert an unresolved project to a project. Used for convenience in build scripts.
    /// </summary>
    /// <param name="unresolved">The unresolved poject</param>
    public static implicit operator Project(UnresolvedProject unresolved) {
        return unresolved.Resolve();
    }
}
