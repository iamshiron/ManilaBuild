namespace Shiron.Manila.API;

/// <summary>
/// Represents a dependency in a project.
/// </summary>
public abstract class Dependency(string type) {
    public readonly string Type = type;

    public abstract void Create(params object[] args);
    public abstract void Resolve(Project project);
}
