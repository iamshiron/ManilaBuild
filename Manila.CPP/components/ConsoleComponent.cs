namespace Shiron.Manila.CPP.Components;

using System.Diagnostics;
using Shiron.Manila.API;
using Shiron.Manila.Attributes;

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
                if (e.Data != null) ApplicationLogger.ApplicationLog(e.Data);
            };
            process.ErrorDataReceived += (sender, e) => {
                if (e.Data != null) ApplicationLogger.ApplicationLog(e.Data);
            };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();

            if (process.ExitCode != 0) throw new Exception("Process exited with code " + process.ExitCode);
        }
    }
}
