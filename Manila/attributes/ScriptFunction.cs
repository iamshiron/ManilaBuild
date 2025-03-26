namespace Shiron.Manila.Attributes;

/// <summary>
/// Represents a function that can be called from the scripting environment.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class ScriptFunction : Attribute {
}
