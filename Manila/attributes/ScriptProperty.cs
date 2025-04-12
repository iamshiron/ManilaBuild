namespace Shiron.Manila.Attributes;

/// <summary>
/// Represents a property that can be accessed from the scripting environment.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class ScriptProperty : Attribute {
    /// <summary>
    /// Disallows the property from being modified.
    /// </summary>
    public readonly bool Immutable;
    /// <summary>
    /// The name of the property as it will be exposed to the script context.
    /// </summary>
    public readonly string? ExposedName;
    /// <summary>
    /// The name of the getter method as it will be exposed to the script context.
    /// </summary>
    public readonly string? GetterName;
    /// <summary>
    /// The name of the setter method as it will be exposed to the script context.
    /// </summary>
    public readonly string? SetterName;

    public ScriptProperty(bool immutable = false, string? getterName = null, string? setterName = null, string? exposedName = null) {
        this.Immutable = immutable;
        this.GetterName = getterName;
        this.SetterName = setterName;
        this.ExposedName = exposedName;
    }
}
