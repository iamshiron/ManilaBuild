using System.Diagnostics;
using System.Text;

namespace Shiron.Manila.Utils;

public static class ShellUtils {
    public static int Run(string command, string? workingDir = null) {
        return Run(command[0..command.IndexOf(' ')], command[command.IndexOf(' ')..].Split(" "), workingDir);
    }
    public static int Run(string command, string args, string? workingDir = null) {
        return Run(command, args.Split(" "), workingDir);
    }
    public static int Run(string command, string[] args, string? workingDir = null) {
        return Run(command, args, workingDir, null, null);
    }

    public static int Run(string command, string[] args, string? workingDir = null, Action<string>? stdOut = null, Action<string>? stdErr = null) {
        if (workingDir == null) workingDir = Directory.GetCurrentDirectory();
        Logger.Debug("Running command: " + command + " " + string.Join(" ", args) + " in " + workingDir);

        var startInfo = new ProcessStartInfo() {
            FileName = command,
            Arguments = string.Join(" ", args),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = workingDir
        };
        startInfo.Environment["TERM"] = "xterm-256color";
        startInfo.Environment["FORCE_COLOR"] = "true";

        using var process = new Process { StartInfo = startInfo };
        StringBuilder stdOutBuilder = new();
        StringBuilder stdErrBuilder = new();

        process.OutputDataReceived += (sender, e) => {
            if (e.Data == null) return;
            stdOutBuilder.AppendLine(e.Data);
            if (stdOut != null) stdOut(e.Data);
        };

        process.ErrorDataReceived += (sender, e) => {
            if (e.Data == null) return;
            stdErrBuilder.AppendLine(e.Data);
            if (stdErr != null) stdErr(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        return process.ExitCode;
    }
}
