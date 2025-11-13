using System.ComponentModel;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Logging;
using Spectre.Console.Cli;

namespace Shiron.Manila.CLI.Commands;

/// <summary>
/// Default command (prints help)
/// </summary>
public sealed class DefaultCommand(BaseServiceContainer baseServices) : BaseManilaCommand<DefaultCommandSettings>(baseServices) {
    /// <summary>
    /// Executes default command and prints global help
    /// </summary>
    protected override int ExecuteCommand(CommandContext context, DefaultCommandSettings settings) {
        return ManilaCli.CommandApp == null
            ? throw new ManilaException("CommandApp is not initialized.")
            : ManilaCli.CommandApp.Run(["--help"]);
    }
}

/// <summary>
/// Global command settings shared across CLI
/// </summary>
public class DefaultCommandSettings : CommandSettings {
    /// <summary>
    /// Use structured JSON logging
    /// </summary>
    [Description("Enables structured logging, outputting in JSON format")]
    [CommandOption("--json")]
    [DefaultValue(false)]
    public bool Structured { get; set; }

    /// <summary>
    /// Enable verbose logging
    /// </summary>
    [Description("Enables verbose logging, does not affect structured logging")]
    [CommandOption("-v|--verbose")]
    [DefaultValue(false)]
    public bool Verbose { get; set; }

    /// <summary>
    /// Suppress non-error logs
    /// </summary>
    [Description("Disables all logs, does not work with structured logging")]
    [CommandOption("-q|--quiet")]
    [DefaultValue(false)]
    public bool Quiet { get; set; }

    /// <summary>
    /// Include stack traces in errors
    /// </summary>
    [Description("Prints a stack trace instead of just the error header")]
    [CommandOption("--stack-trace")]
    [DefaultValue(false)]
    public bool StackTrace { get; set; }

    /// <summary>
    /// Log profiling events
    /// </summary>
    [Description("Logs profiling information during command execution")]
    [CommandOption("--log-profiling")]
    public bool LogProfiling { get; set; } = false;

    /// <summary>
    /// Invalidate build caches
    /// </summary>
    [Description("Invalidates caches to force a rebuild of the workspace")]
    [CommandOption("--api-invalidate-build-cache")]
    [DefaultValue(false)]
    public bool APIInvalidateBuildCache { get; set; }

    /// <summary>
    /// Converts settings to log options
    /// </summary>
    public LogOptions ToLogOptions() {
        return new LogOptions(Quiet, Verbose, Structured, StackTrace, LogProfiling);
    }
}
