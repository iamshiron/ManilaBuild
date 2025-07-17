using System;
using Shiron.Manila.CLI.Commands;
using Shiron.Manila.Exceptions;
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
    public static int HandleException(Exception exception, DefaultCommandSettings settings) {
        return exception switch {
            ScriptingException e => HandleScriptingException(e, settings),
            BuildException e => HandleBuildException(e, settings),
            ConfigurationException e => HandleConfigurationException(e, settings),
            ManilaException e => HandleManilaException(e, settings),
            _ => HandleUnknownException(exception, settings)
        };
    }

    /// <summary>
    /// Safely executes a command function and handles any exceptions that occur.
    /// </summary>
    /// <param name="commandFunction">The command function to execute</param>
    /// <param name="settings">Command settings for error handling</param>
    /// <returns>Exit code - 0 for success, negative values for errors</returns>
    public static int SafeExecute(Func<int> commandFunction, DefaultCommandSettings settings) {
        try {
            return commandFunction();
        } catch (Exception e) {
            return HandleException(e, settings);
        }
    }

    /// <summary>
    /// Safely executes an async command function and handles any exceptions that occur.
    /// </summary>
    /// <param name="commandFunction">The async command function to execute</param>
    /// <param name="settings">Command settings for error handling</param>
    /// <returns>Exit code - 0 for success, negative values for errors</returns>
    public static async Task<int> SafeExecuteAsync(Func<Task<int>> commandFunction, DefaultCommandSettings settings) {
        try {
            return await commandFunction();
        } catch (AggregateException ae) {
            var innerException = ae.InnerException ?? ae;
            return HandleException(innerException, settings);
        } catch (Exception e) {
            return HandleException(e, settings);
        }
    }

    private static int HandleScriptingException(ScriptingException e, DefaultCommandSettings settings) {
        // Extract the most relevant error message, preferring inner exception details if available
        string errorMessage = e.Message;

        if (e.InnerException != null) {
            // Check if inner exception has more detailed error information
            if (e.InnerException is Microsoft.ClearScript.ScriptEngineException see && !string.IsNullOrEmpty(see.ErrorDetails)) {
                errorMessage = see.ErrorDetails;
            } else if (!string.IsNullOrEmpty(e.InnerException.Message)) {
                errorMessage = e.InnerException.Message;
            }
        }

        AnsiConsole.MarkupLine($"\n[red]{Emoji.Known.CrossMark} Script Error:[/] [white]{Markup.Escape(errorMessage)}[/]");
        AnsiConsole.MarkupLine("[grey]This error occurred while executing a script. Check the script for syntax errors or logic issues.[/]");
        AnsiConsole.MarkupLine("[grey]Run with --stack-trace for a detailed technical log.[/]");

        if (settings.StackTrace) {
            Utils.TryWriteException(e.InnerException ?? e);
        }

        return ExitCodes.SCRIPTING_ERROR;
    }

    private static int HandleBuildException(BuildException e, DefaultCommandSettings settings) {
        AnsiConsole.MarkupLine($"\n[red]{Emoji.Known.CrossMark} Build Error:[/] [white]{Markup.Escape(e.Message)}[/]");
        AnsiConsole.MarkupLine("[grey]The project failed to build. Review the build configuration and source files for errors.[/]");
        AnsiConsole.MarkupLine("[grey]Run with --stack-trace for a detailed technical log.[/]");

        if (settings.StackTrace) {
            Utils.TryWriteException(e.InnerException ?? e);
        }

        return ExitCodes.BUILD_ERROR;
    }

    private static int HandleConfigurationException(ConfigurationException e, DefaultCommandSettings settings) {
        AnsiConsole.MarkupLine($"\n[yellow]{Emoji.Known.Warning} Configuration Error:[/] [white]{Markup.Escape(e.Message)}[/]");
        AnsiConsole.MarkupLine("[grey]There is a problem with a configuration file or setting. Please verify it is correct.[/]");
        AnsiConsole.MarkupLine("[grey]Run with --stack-trace for more technical details.[/]");

        if (settings.StackTrace) {
            Utils.TryWriteException(e.InnerException ?? e);
        }

        return ExitCodes.CONFIGURATION_ERROR;
    }

    private static int HandleManilaException(ManilaException e, DefaultCommandSettings settings) {
        AnsiConsole.MarkupLine($"\n[yellow]{Emoji.Known.Warning} Application Error:[/] [white]{Markup.Escape(e.Message)}[/]");
        AnsiConsole.MarkupLine($"[grey]A known issue ('{e.GetType().Name}') occurred. This is a handled error condition.[/]");
        AnsiConsole.MarkupLine("[grey]Run with --stack-trace for more technical details.[/]");

        if (settings.StackTrace) {
            Utils.TryWriteException(e);
        }

        return ExitCodes.KNOWN_ERROR;
    }

    private static int HandleUnknownException(Exception e, DefaultCommandSettings settings) {
        AnsiConsole.MarkupLine($"\n[red]{Emoji.Known.Collision} Unexpected System Exception:[/] [white]{Markup.Escape(e.GetType().Name)}[/]");
        AnsiConsole.MarkupLine("[red]This may indicate a bug in the application. Please report this issue.[/]");
        AnsiConsole.MarkupLine("[grey]Run with --stack-trace for a detailed error log.[/]");

        if (settings.StackTrace) {
            Utils.TryWriteException(e);
        }

        return ExitCodes.UNKNOWN_ERROR;
    }
}
