using Shiron.Manila.API.Exceptions;
using Shiron.Manila.Logging;
using Spectre.Console;

namespace Shiron.Manila.CLI;

/// <summary>
/// Centralized error handling for the Manila CLI application.
/// Provides consistent error formatting and exit codes across all commands.
/// </summary>
public static class ErrorHandler {
    /// <summary>
    /// Handles any exception that occurs during command execution and returns the appropriate exit code.
    /// </summary>
    /// <param name="exception">The exception to handle</param>
    /// <param name="settings">Command settings for determining output verbosity</param>
    /// <returns>The appropriate exit code based on the exception type</returns>
    public static int Handle(ILogger logger, Exception exception, LogOptions settings) {
        return exception switch {
            ScriptExecutionException e => HandleScriptExecutionException(logger, e, settings),
            BuildProcessException e => HandleBuildProcessException(logger, e, settings),
            ScriptCompilationException e => HandleScriptCompilationException(logger, e, settings),
            ConfigurationException e => HandleConfigurationException(logger, e, settings),
            PluginException e => HandlePluginException(logger, e, settings),
            InternalLogicException e => HandleInternalLogicException(logger, e, settings),
            ManilaException e => HandleManilaException(logger, e, settings),
            _ => HandleException(logger, exception, settings)
        };
    }

    /// <summary>
    /// Safely executes a command function and handles any exceptions that occur.
    /// </summary>
    /// <param name="commandFunction">The command function to execute</param>
    /// <param name="settings">Command settings for error handling</param>
    /// <returns>Exit code - 0 for success, negative values for errors</returns>
    public static int SafeExecute(ILogger logger, Func<int> commandFunction, LogOptions settings) {
        try {
            return commandFunction();
        } catch (Exception e) {
            return Handle(logger, e, settings);
        }
    }

    /// <summary>
    /// Safely executes an async command function and handles any exceptions that occur.
    /// </summary>
    /// <param name="commandFunction">The async command function to execute</param>
    /// <param name="settings">Command settings for error handling</param>
    /// <returns>Exit code - 0 for success, negative values for errors</returns>
    public static async Task<int> SafeExecuteAsync(ILogger logger, Func<Task<int>> commandFunction, LogOptions settings) {
        try {
            return await commandFunction();
        } catch (AggregateException ae) {
            var innerException = ae.InnerException ?? ae;
            return Handle(logger, innerException, settings);
        } catch (Exception e) {
            return Handle(logger, e, settings);
        }
    }

    private static int HandleScriptCompilationException(ILogger logger, ScriptCompilationException e, LogOptions settings) {
        logger.MarkupLine($"\n[red]{Emoji.Known.CrossMark} Compilation Error:[/] [white]{Markup.Escape(e.Message)}[/]");
        logger.MarkupLine("[grey]There was an error compiling the script. Please check the script syntax and references.[/]");
        logger.MarkupLine($"[grey]Error happened during [white]{ManilaCli.ExecutionStage?.Stage}[/] stage.[/]");
        logger.MarkupLine("[grey]Run with --stack-trace for a detailed technical log.[/]");

        if (settings.StackTrace) {
            ExceptionUtils.PrintException(e.InnerException ?? e);

            logger.MarkupLine($"[red]Compilation Diagnostics: ({e.Diagnostics.Count} Errors)[/]");
            foreach (var d in e.Diagnostics) {
                logger.MarkupLine($"[red]  {d.Id} - {Markup.Escape(d.GetMessage())} - {d.Location.GetLineSpan()}[/]");
            }
        }

        return ExitCodes.SCRIPT_COMPILATION_ERROR;
    }

    private static int HandleConfigurationException(ILogger logger, ConfigurationException e, LogOptions settings) {
        logger.MarkupLine($"\n[yellow]{Emoji.Known.Warning} Configuration Error:[/] [white]{Markup.Escape(e.Message)}[/]");
        logger.MarkupLine("[grey]There is a problem with the configuration. Please check your project files and settings.[/]");
        logger.MarkupLine($"[grey]Error happened during [white]{ManilaCli.ExecutionStage?.Stage}[/] stage.[/]");
        logger.MarkupLine("[grey]Run with --stack-trace for more technical details.[/]");

        if (settings.StackTrace) {
            ExceptionUtils.PrintException(e.InnerException ?? e);
        }

        return ExitCodes.CONFIGURATION_ERROR;
    }

    private static int HandleScriptExecutionException(ILogger logger, ScriptExecutionException e, LogOptions settings) {
        logger.MarkupLine($"Error happened during {ManilaCli.ExecutionStage?.Stage} stage.");
        logger.MarkupLine("[grey]This error occurred while executing a script. Check the script for syntax errors or logic issues.[/]");
        logger.MarkupLine($"[grey]Error happened during [white]{ManilaCli.ExecutionStage?.Stage}[/] stage.[/]");
        logger.MarkupLine("[grey]Run with --stack-trace for a detailed technical log.[/]");

        if (settings.StackTrace) {
            ExceptionUtils.PrintException(e.InnerException ?? e);
        }

        return ExitCodes.SCRIPT_EXECUTION_ERROR;
    }

    private static int HandleBuildProcessException(ILogger logger, BuildProcessException e, LogOptions settings) {
        logger.MarkupLine($"\n[red]{Emoji.Known.CrossMark} Build Error:[/] [white]{Markup.Escape(e.Message)}[/]");
        logger.MarkupLine("[grey]The project failed to build. Review the build configuration and source files for errors.[/]");
        logger.MarkupLine($"[grey]Error happened during [white]{ManilaCli.ExecutionStage?.Stage}[/] stage.[/]");
        logger.MarkupLine("[grey]Run with --stack-trace for a detailed technical log.[/]");

        if (settings.StackTrace) {
            ExceptionUtils.PrintException(e.InnerException ?? e);
        }

        return ExitCodes.BUILD_PROCESS_ERROR;
    }

    private static int HandleInternalLogicException(ILogger logger, InternalLogicException e, LogOptions settings) {
        logger.MarkupLine($"\n[yellow]{Emoji.Known.Warning} Configuration Error:[/] [white]{Markup.Escape(e.Message)}[/]");
        logger.MarkupLine("[grey]There is a problem with a configuration file or setting. Please verify it is correct.[/]");
        logger.MarkupLine($"[grey]Error happened during [white]{ManilaCli.ExecutionStage?.Stage}[/] stage.[/]");
        logger.MarkupLine("[grey]Run with --stack-trace for more technical details.[/]");

        if (settings.StackTrace) {
            ExceptionUtils.PrintException(e.InnerException ?? e);
        }

        return ExitCodes.INTERNAL_LOGIC_ERROR;
    }

    private static int HandlePluginException(ILogger logger, PluginException e, LogOptions settings) {
        logger.MarkupLine($"\n[yellow]{Emoji.Known.Warning} Plugin Error:[/] [white]{Markup.Escape(e.Message)}[/]");
        logger.MarkupLine("[grey]An error occurred in a plugin. Please check the plugin configuration or report this issue.[/]");
        logger.MarkupLine($"[grey]Error happened during [white]{ManilaCli.ExecutionStage?.Stage}[/] stage.[/]");
        logger.MarkupLine("[grey]Run with --stack-trace for more technical details.[/]");

        if (settings.StackTrace) {
            ExceptionUtils.PrintException(e.InnerException ?? e);
        }

        return ExitCodes.PLUGIN_ERROR;
    }

    private static int HandleManilaException(ILogger logger, ManilaException e, LogOptions settings) {
        logger.MarkupLine($"\n[yellow]{Emoji.Known.Warning} Application Error:[/] [white]{Markup.Escape(e.Message)}[/]");
        logger.MarkupLine($"[grey]A known issue ('{e.GetType().Name}') occurred. This is a handled error condition.[/]");
        logger.MarkupLine($"[grey]Error happened during [white]{ManilaCli.ExecutionStage?.Stage}[/] stage.[/]");
        logger.MarkupLine("[grey]Run with --stack-trace for more technical details.[/]");

        if (settings.StackTrace) {
            ExceptionUtils.PrintException(e);
        }

        return ExitCodes.ANY_KNOWN_ERROR;
    }

    private static int HandleException(ILogger logger, Exception e, LogOptions settings) {
        logger.MarkupLine($"\n[red]{Emoji.Known.Collision} Unexpected System Exception:[/] [white]{Markup.Escape(e.GetType().Name)}[/]");
        logger.MarkupLine("[red]This may indicate a bug in the application. Please report this issue.[/]");
        logger.MarkupLine($"[grey]Error happened during [white]{ManilaCli.ExecutionStage?.Stage}[/] stage.[/]");
        logger.MarkupLine("[grey]Run with --stack-trace for a detailed error log.[/]");

        if (settings.StackTrace) {
            ExceptionUtils.PrintException(e);
        }

        return ExitCodes.UNKNOWN_ERROR;
    }
}
