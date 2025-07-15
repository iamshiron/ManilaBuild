
using System.Diagnostics;
using Shiron.Manila.API;
using Shiron.Manila.Attributes;
using Shiron.Manila.Utils;

namespace Shiron.Manila.CPP.Components;
/// <summary>
/// Represents a C++ console application project.
/// </summary>
public class ConsoleComponent : CppComponent {
    public ConsoleComponent() : base("console") {
    }

    [ScriptProperty]
    public DirHandle? RunDir { get; set; }

    public override void Run(Project project) {
        throw new NotImplementedException();
    }
}
