namespace Shiron.Manila.Attributes;

/// <summary>
/// This attribute is used to mark a class as a script enum. Script enums are used to be exposed to the scripting environment.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class ScriptEnum : Attribute {
}
