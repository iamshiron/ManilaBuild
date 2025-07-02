namespace Shiron.Manila.CPP.Components;

using System.Diagnostics;
using Shiron.Manila.API;
using Shiron.Manila.Attributes;
using Shiron.Manila.Logging;

/// <summary>
/// Represents a C++ console application project.
/// </summary>
public class ConsoleComponent : CppComponent {
    public ConsoleComponent() : base("console") {
    }

    [ScriptProperty]
    public DirHandle? RunDir { get; set; }

    public override void Run(Project project) {
        var instance = ManilaCPP.Instance!;
        ShellUtils.Run(project.GetComponent<ConsoleComponent>().BinDir + "/" + project.Name + ".exe");

        /*
        instance.Debug("Running project: " + project.Name);
        instance.Debug("BinDir: " + project.GetComponent<ConsoleComponent>().BinDir);

        var startInfo = new ProcessStartInfo() {
            FileName = project.GetComponent<ConsoleComponent>().BinDir + "/" + project.Name + ".exe",
            Arguments = "",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using (Process process = Process.Start(startInfo)) {
            process.OutputDataReceived += (sender, e) => {
                if (e.Data != null) ManilaCPP.Instance.Info(e.Data);
            };
            process.ErrorDataReceived += (sender, e) => {
                if (e.Data != null) ManilaCPP.Instance.Info(e.Data);
            };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();

            if (process.ExitCode != 0) throw new Exception("Process exited with code " + process.ExitCode);
        }
        */
    }
}
