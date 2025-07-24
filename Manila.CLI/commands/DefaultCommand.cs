using System.ComponentModel;
using Shiron.Manila.Logging;
using Spectre.Console.Cli;

namespace Shiron.Manila.CLI.Commands;

public sealed class DefaultCommand(ILogger logger) : BaseManilaCommand<DefaultCommandSettings>(logger) {
    protected override int ExecuteCommand(CommandContext context, DefaultCommandSettings settings) {
        return ManilaCli.CommandApp == null
            ? throw new ManilaException("CommandApp is not initialized.")
            : ManilaCli.CommandApp.Run(["--help"]);
    }
}

public class DefaultCommandSettings : CommandSettings {
    [Description("Enables structured logging, outputting in JSON format")]
    [CommandOption("--json")]
    [DefaultValue(false)]
    public bool Structured { get; set; }

    [Description("Enables verbose logging, does not affect structured logging")]
    [CommandOption("-v|--verbose")]
    [DefaultValue(false)]
    public bool Verbose { get; set; }

    [Description("Disables all logs, does not work with structured logging")]
    [CommandOption("-q|--quiet")]
    [DefaultValue(false)]
    public bool Quiet { get; set; }

    [Description("Prints a stack trace instead of just the error header")]
    [CommandOption("--stack-trace")]
    [DefaultValue(false)]
    public bool StackTrace { get; set; }

    public LogOptions ToLogOptions() {
        return new LogOptions(Quiet, Verbose, Structured, StackTrace);
    }
}
