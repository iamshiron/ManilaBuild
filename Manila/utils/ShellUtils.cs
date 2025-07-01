using System.Diagnostics;
using System.Text;

namespace Shiron.Manila.Logging;

/// <summary>
/// Provides utility methods for executing shell commands and processes.
/// </summary>
public static class ShellUtils {
    /// <summary>
    /// Executes a shell command with optional working directory.
    /// </summary>
    /// <param name="command">The full command string including arguments.</param>
    /// <param name="workingDir">Optional working directory where the command will be executed. If null, current directory is used.</param>
    /// <returns>The exit code of the process.</returns>
    public static int Run(string command, string? workingDir = null) {
        return Run(command[0..command.IndexOf(' ')], command[command.IndexOf(' ')..].Split(" "), workingDir);
    }
    /// <summary>
    /// Executes a shell command with specified arguments and optional working directory.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="args">The arguments as a single string.</param>
    /// <param name="workingDir">Optional working directory where the command will be executed. If null, current directory is used.</param>
    /// <returns>The exit code of the process.</returns>
    public static int Run(string command, string args, string? workingDir = null) {
        return Run(command, args.Split(" "), workingDir);
    }
    /// <summary>
    /// Executes a shell command with specified arguments array and optional working directory.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="args">The arguments as an array of strings.</param>
    /// <param name="workingDir">Optional working directory where the command will be executed. If null, current directory is used.</param>
    /// <returns>The exit code of the process.</returns>
    public static int Run(string command, string[] args, string? workingDir = null) {
        return Run(command, args, workingDir, null, null);
    }

    /// <summary>
    /// Executes a shell command and logs output to the application logger.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="args">The arguments as an array of strings.</param>
    /// <param name="workingDir">Optional working directory where the command will be executed. If null, current directory is used.</param>
    /// <param name="logStdOut">Whether to log standard output to the application logger.</param>
    /// <param name="logStdErr">Whether to log standard error to the application logger.</param>
    /// <returns>The exit code of the process.</returns>
    public static int ApplicationRun(string command, string[] args, string? workingDir = null, bool logStdOut = true, bool logStdErr = true) {
        return Run(command, args, workingDir, logStdOut ? (s) => Console.WriteLine(s) : null, logStdErr ? (s) => Console.WriteLine(s) : null);
    }
    /// <summary>
    /// Executes a shell command with arguments as a string and logs output to the application logger.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="args">The arguments as a single string.</param>
    /// <param name="workingDir">Optional working directory where the command will be executed. If null, current directory is used.</param>
    /// <param name="logStdOut">Whether to log standard output to the application logger.</param>
    /// <param name="logStdErr">Whether to log standard error to the application logger.</param>
    /// <returns>The exit code of the process.</returns>
    public static int ApplicationRun(string command, string args, string? workingDir = null, bool logStdOut = true, bool logStdErr = true) {
        return ApplicationRun(command, args.Split(" "), workingDir, logStdOut, logStdErr);
    }
    /// <summary>
    /// Executes a shell command with arguments included in the command string and logs output to the application logger.
    /// </summary>
    /// <param name="command">The full command string including arguments.</param>
    /// <param name="workingDir">Optional working directory where the command will be executed. If null, current directory is used.</param>
    /// <param name="logStdOut">Whether to log standard output to the application logger.</param>
    /// <param name="logStdErr">Whether to log standard error to the application logger.</param>
    /// <returns>The exit code of the process.</returns>
    public static int ApplicationRun(string command, string? workingDir = null, bool logStdOut = true, bool logStdErr = true) {
        return ApplicationRun(command[0..command.IndexOf(' ')], command[command.IndexOf(' ')..].Split(" "), workingDir, logStdOut, logStdErr);
    }

    /// <summary>
    /// Executes a shell command with full control over process configuration and output handling.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="args">The arguments as an array of strings.</param>
    /// <param name="workingDir">Optional working directory where the command will be executed. If null, current directory is used.</param>
    /// <param name="stdOut">Optional callback action to handle standard output lines.</param>
    /// <param name="stdErr">Optional callback action to handle standard error lines.</param>
    /// <returns>The exit code of the process.</returns>
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
