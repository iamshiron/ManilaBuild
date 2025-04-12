namespace Shiron.Manila.API;

/// <summary>
/// Represents a dependency in a project.
/// </summary>
public abstract class Dependency {
    public readonly string Type;

    public Dependency(string type) {
        this.Type = type;
    }

    public abstract void Create(params object[] args);
    public abstract void Resolve();
}
