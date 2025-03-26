namespace Shiron.Manila.Attributes;

/// <summary>
/// Represents a component of a plugin.
/// </summary>
public abstract class PluginComponent(string name) {
    /// <summary>
    /// The name of the component.
    /// </summary>
    public readonly string Name = name;
    /// <summary>
    /// The plugin this component is registered to. Only set after registration.
    /// </summary>
    internal ManilaPlugin? plugin;

    public override string ToString() {
        return $"PluginComponent({Name})";
    }
}
