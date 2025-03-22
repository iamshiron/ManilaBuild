namespace Shiron.Manila.CPP.Components;

using Shiron.Manila.API;
using Shiron.Manila.Attributes;
using Shiron.Manila.Attributes;

public class ConsoleComponent : CppComponent {
    public ConsoleComponent() : base("console") {
    }

    [ScriptProperty]
    public Dir runDir { get; set; }
}
