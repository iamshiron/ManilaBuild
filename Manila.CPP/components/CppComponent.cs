namespace Shiron.Manila.CPP.Components;

using Shiron.Manila.API;
using Shiron.Manila.Attributes;

/// <summary>
/// Represents a C++ project.
/// </summary>
public class CppComponent : PluginComponent {
    public CppComponent(string name) : base(name) {
    }

    [ScriptProperty]
    public Dir? BinDir { get; set; }
    [ScriptProperty]
    public Dir? ObjDir { get; set; }
    [ScriptProperty]
    public EToolChain? ToolChain { get; set; }
}
