namespace Shiron.Manila.CPP.Components;

using Shiron.Manila.API;
using Shiron.Manila.Attributes;

/// <summary>
/// Represents a C++ console application project.
/// </summary>
public class ConsoleComponent : CppComponent {
    public ConsoleComponent() : base("console") {
    }

    [ScriptProperty]
    public Dir? RunDir { get; set; }
}
