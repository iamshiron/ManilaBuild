using System;
using Shiron.Manila.CLI.Commands;
using Shiron.Manila.Exceptions;
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
    public static int ManilaException(ILogger logger, Exception exception, LogOptions settings) {
        return exception switch {
            ScriptingException e => ManilaException(logger, e, settings),
            BuildTimeException e => ManilaException(logger, e, settings),
            ConfigurationTimeException e => ManilaException(logger, e, settings),
            PluginException e => ManilaException(logger, e, settings),
            RuntimeException e => ManilaException(logger, e, settings),
            ManilaException e => ManilaException(logger, e, settings),
            _ => ManilaException(logger, exception, settings)
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
            return ManilaException(logger, e, settings);
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
            return ManilaException(logger, innerException, settings);
        } catch (Exception e) {
            return ManilaException(logger, e, settings);
        }
    }

    private static int ManilaException(ILogger logger, ScriptingException e, LogOptions settings) {
        string errorMessage = e.Message;

        if (e.InnerException != null) {
            if (e.InnerException is Microsoft.ClearScript.ScriptEngineException see && !string.IsNullOrEmpty(see.ErrorDetails)) {
                errorMessage = see.ErrorDetails;
            } else if (!string.IsNullOrEmpty(e.InnerException.Message)) {
                errorMessage = e.InnerException.Message;
            }
        }

        logger.MarkupLine($"\n[red]{Emoji.Known.CrossMark} Script Error:[/] [white]{Markup.Escape(errorMessage)}[/]");
        logger.MarkupLine("[grey]This error occurred while executing a script. Check the script for syntax errors or logic issues.[/]");
        logger.MarkupLine("[grey]Run with --stack-trace for a detailed technical log.[/]");

        if (settings.StackTrace) {
            ExceptionUtils.ManilaException(e.InnerException ?? e);
        }

        return ExitCodes.SCRIPTING_ERROR;
    }

    private static int ManilaException(ILogger logger, BuildTimeException e, LogOptions settings) {
        logger.MarkupLine($"\n[red]{Emoji.Known.CrossMark} Build Error:[/] [white]{Markup.Escape(e.Message)}[/]");
        logger.MarkupLine("[grey]The project failed to build. Review the build configuration and source files for errors.[/]");
        logger.MarkupLine("[grey]Run with --stack-trace for a detailed technical log.[/]");

        if (settings.StackTrace) {
            ExceptionUtils.ManilaException(e.InnerException ?? e);
        }

        return ExitCodes.BUILD_ERROR;
    }

    private static int ManilaException(ILogger logger, ConfigurationTimeException e, LogOptions settings) {
        logger.MarkupLine($"\n[yellow]{Emoji.Known.Warning} Configuration Error:[/] [white]{Markup.Escape(e.Message)}[/]");
        logger.MarkupLine("[grey]There is a problem with a configuration file or setting. Please verify it is correct.[/]");
        logger.MarkupLine("[grey]Run with --stack-trace for more technical details.[/]");

        if (settings.StackTrace) {
            ExceptionUtils.ManilaException(e.InnerException ?? e);
        }

        return ExitCodes.CONFIGURATION_ERROR;
    }

    private static int ManilaException(ILogger logger, RuntimeException e, LogOptions settings) {
        logger.MarkupLine($"\n[red]{Emoji.Known.CrossMark} Runtime Error:[/] [white]{Markup.Escape(e.Message)}[/]");
        logger.MarkupLine("[grey]An unexpected error occurred during execution. This may indicate a bug in the application.[/]");
        logger.MarkupLine("[grey]Run with --stack-trace for a detailed error log.[/]");

        if (settings.StackTrace) {
            ExceptionUtils.ManilaException(e.InnerException ?? e);
        }

        return ExitCodes.RUNTIME_ERROR;
    }

    private static int ManilaException(ILogger logger, PluginException e, LogOptions settings) {
        logger.MarkupLine($"\n[yellow]{Emoji.Known.Warning} Plugin Error:[/] [white]{Markup.Escape(e.Message)}[/]");
        logger.MarkupLine("[grey]An error occurred in a plugin. Please check the plugin configuration or report this issue.[/]");
        logger.MarkupLine("[grey]Run with --stack-trace for more technical details.[/]");

        if (settings.StackTrace) {
            ExceptionUtils.ManilaException(e.InnerException ?? e);
        }

        return ExitCodes.PLUGIN_ERROR;
    }

    private static int ManilaException(ILogger logger, ManilaException e, LogOptions settings) {
        logger.MarkupLine($"\n[yellow]{Emoji.Known.Warning} Application Error:[/] [white]{Markup.Escape(e.Message)}[/]");
        logger.MarkupLine($"[grey]A known issue ('{e.GetType().Name}') occurred. This is a handled error condition.[/]");
        logger.MarkupLine("[grey]Run with --stack-trace for more technical details.[/]");

        if (settings.StackTrace) {
            ExceptionUtils.ManilaException(e);
        }

        return ExitCodes.ANY_KNOWN_ERROR;
    }

    private static int ManilaException(ILogger logger, Exception e, LogOptions settings) {
        logger.MarkupLine($"\n[red]{Emoji.Known.Collision} Unexpected System Exception:[/] [white]{Markup.Escape(e.GetType().Name)}[/]");
        logger.MarkupLine("[red]This may indicate a bug in the application. Please report this issue.[/]");
        logger.MarkupLine("[grey]Run with --stack-trace for a detailed error log.[/]");

        if (settings.StackTrace) {
            ExceptionUtils.ManilaException(e);
        }

        return ExitCodes.UNKNOWN_ERROR;
    }
}
