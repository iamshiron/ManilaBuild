using System.Diagnostics;
using System.Text;
using Shiron.Manila.Logging;

namespace Shiron.Manila.Utils;

/// <summary>
/// Provides utility methods for executing shell commands and processes.
/// </summary>
public static class ShellUtils {
    /// <summary>
    /// Encapsulates all the information required to execute a shell command.
    /// </summary>
    /// <param name="command">The command or executable to run.</param>
    /// <param name="args">The arguments to pass to the command. Defaults to an empty array if null.</param>
    /// <param name="workingDir">The working directory for the command. If null, the current directory is used.</param>
    /// <param name="quiet">If true, logs will be marked as quiet, indicating the command's output is not high-priority. It is still logged.</param>
    /// <param name="suppress">If true, standard output and standard error will not be logged. Use this for commands that may handle sensitive data.</param>
    public sealed class CommandInfo(string command, string[]? args = null, string? workingDir = null, bool quiet = false, bool suppress = false) {
        /// <summary>
        /// The command or executable to run.
        /// </summary>
        public string Command = command;
        /// <summary>
        /// The arguments to pass to the command.
        /// </summary>
        public string[] Args = args ?? [];
        /// <summary>
        /// The working directory for the command.
        /// </summary>
        public string? WorkingDir = workingDir;
        /// <summary>
        /// When true, logs are still created but are flagged as low-priority.
        /// </summary>
        public bool Quiet = quiet;
        /// <summary>
        /// When true, the output is not logged, which is useful for commands handling sensitive data.
        /// For security reasons, this is marked as readonly.
        /// </summary>
        public readonly bool Suppress = suppress;
    }

    /// <summary>
    /// Executes a shell command with an optional working directory.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="args">The command arguments.</param>
    /// <param name="workingDir">Optional working directory where the command will be executed. If null, the current directory is used.</param>
    /// <returns>The exit code of the process.</returns>
    public static int Run(string command, string[]? args = null, string? workingDir = null) {
        return Run(new(command, args, workingDir));
    }

    /// <summary>
    /// Runs a command with suppressed output logging. Standard output and error will not be logged.
    /// </summary>
    /// <remarks>
    /// This is a convenience method that sets both <see cref="CommandInfo.Quiet"/> and <see cref="CommandInfo.Suppress"/> to true.
    /// </remarks>
    /// <param name="command">The command to execute.</param>
    /// <param name="args">The command arguments.</param>
    /// <param name="workingDir">Optional working directory where the command will be executed. If null, the current directory is used.</param>
    /// <returns>The exit code of the process.</returns>
    public static int RunSuppressed(string command, string[]? args, string? workingDir = null) {
        return Run(new(command, args, workingDir, true, true));
    }

    /// <summary>
    /// Executes a shell command using the provided <see cref="CommandInfo"/>.
    /// </summary>
    /// <remarks>
    /// This is the core execution method. It sets up the process, captures standard output and error,
    /// logs the execution lifecycle (start, output, finish/fail), and waits for the command to complete.
    /// Environment variables `TERM` and `FORCE_COLOR` are set to ensure consistent output formatting.
    /// </remarks>
    /// <param name="info">An object containing all configuration for the command execution.</param>
    /// <returns>The exit code of the process.</returns>
    public static int Run(CommandInfo info) {
        var workingDir = info.WorkingDir ?? Directory.GetCurrentDirectory();
        var contextID = Guid.NewGuid();
        var startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        Logger.Log(new CommandExecutionLogEntry(contextID, info.Command, info.Args, workingDir));

        var startInfo = new ProcessStartInfo() {
            FileName = info.Command,
            Arguments = string.Join(" ", info.Args),
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
            if (!info.Suppress) Logger.Log(new CommandStdOutLogEntry(contextID, e.Data, info.Quiet));
        };

        process.ErrorDataReceived += (sender, e) => {
            if (e.Data == null) return;
            stdErrBuilder.AppendLine(e.Data);
            if (!info.Suppress) Logger.Log(new CommandStdErrLogEntry(contextID, e.Data, info.Quiet));
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        if (process.ExitCode == 0) {
            Logger.Log(new CommandExecutionFinishedLogEntry(
                contextID, stdOutBuilder.ToString(), stdErrBuilder.ToString(), DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startTime, process.ExitCode
            ));
        } else {
            Logger.Log(new CommandExecutionFailedLogEntry(
                contextID, stdOutBuilder.ToString(), stdErrBuilder.ToString(), DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startTime, process.ExitCode
            ));
        }

        return process.ExitCode;
    }
}
