namespace Shiron.Manila.API;

/// <summary>
/// Represents a dependency in a project.
/// </summary>
public abstract class Dependency {
    public readonly string Name;

    public Dependency(string name) {
        this.Name = name;
    }

    public abstract void Create(params object[] args);
    public abstract void Resolve();
}
