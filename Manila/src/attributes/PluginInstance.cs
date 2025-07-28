namespace Shiron.Manila.Attributes;

/// <summary>
/// This attribute is used to mark a property as a global plugin singleton instance.
/// Gets automatically set by the plugin manager.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class PluginInstance : Attribute {
}
