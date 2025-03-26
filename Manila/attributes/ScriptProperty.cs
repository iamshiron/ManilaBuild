namespace Shiron.Manila.Attributes;

/// <summary>
/// Represents a property that can be accessed from the scripting environment.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class ScriptProperty : Attribute {
    /// <summary>
    /// Disallows the property from being modified.
    /// </summary>
    public readonly bool immutable;

    public ScriptProperty(bool immutable = false) {
        this.immutable = immutable;
    }
}
