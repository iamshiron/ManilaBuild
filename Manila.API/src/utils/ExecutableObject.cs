
namespace Shiron.Manila.API.Utils;

/// <summary>
/// Represents a base class for any object that can be executed within a graph.
/// </summary>
public abstract class ExecutableObject {
    // The ID is cached to ensure it's consistent for each object instance.
    public Guid ExecutableID { get; } = Guid.NewGuid();

    public virtual bool IsBlocking() { return true; }

    // Returns the cached ID for the object.
    public virtual string GetID() { return ExecutableID.ToString(); }

    public abstract Task RunAsync();

    /// <summary>
    /// Provides a string representation of the executable object.
    /// </summary>
    /// <returns>The type name and the first 8 characters of its unique ID.</returns>
    public override string ToString() {
        return $"{GetType().Name} [{GetID().AsSpan(0, 8)}]";
    }

    public override bool Equals(object? obj) {
        return obj is ExecutableObject o && o.GetID() == GetID();
    }

    /// <summary>
    /// Gets the hash code for the object, based on its unique ID.
    /// This is crucial for performance when the object is used as a key in a dictionary.
    /// </summary>
    /// <returns>The hash code of the object's ID.</returns>
    public override int GetHashCode() {
        return GetID().GetHashCode();
    }
}
