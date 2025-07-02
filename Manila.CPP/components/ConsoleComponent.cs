namespace Shiron.Manila.CPP.Components;

using System.Diagnostics;
using Shiron.Manila.API;
using Shiron.Manila.Attributes;
using Shiron.Manila.Utils;

/// <summary>
/// Represents a C++ console application project.
/// </summary>
public class ConsoleComponent : CppComponent {
    public ConsoleComponent() : base("console") {
    }

    [ScriptProperty]
    public DirHandle? RunDir { get; set; }

    public override void Run(Project project) {
        ShellUtils.Run(project.GetComponent<ConsoleComponent>().BinDir + "/" + project.Name + ".exe");
    }
}
