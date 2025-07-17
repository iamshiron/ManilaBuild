using Shiron.Manila.CLI.Commands;
using Spectre.Console.Cli;

namespace Shiron.Manila.CLI;

/// <summary>
/// Base class for all Manila CLI commands that provides centralized error handling.
/// </summary>
/// <typeparam name="TSettings">The settings type for the command</typeparam>
public abstract class BaseManilaCommand<TSettings> : Command<TSettings>
    where TSettings : DefaultCommandSettings {

    /// <summary>
    /// Final execute method that wraps the command execution with error handling.
    /// Override ExecuteCommand instead of this method.
    /// </summary>
    public sealed override int Execute(CommandContext context, TSettings settings) {
        return ErrorHandler.SafeExecute(() => ExecuteCommand(context, settings), settings);
    }

    /// <summary>
    /// Override this method to implement your command logic.
    /// Any exceptions thrown will be automatically handled by the ErrorHandler.
    /// </summary>
    /// <param name="context">The command context</param>
    /// <param name="settings">The command settings</param>
    /// <returns>Exit code - 0 for success, negative values for errors</returns>
    protected abstract int ExecuteCommand(CommandContext context, TSettings settings);
}

/// <summary>
/// Base class for all Manila CLI commands that provides centralized error handling.
/// </summary>
/// <typeparam name="TSettings">The settings type for the command</typeparam>
public abstract class BaseAsyncManilaCommand<TSettings> : AsyncCommand<TSettings>
    where TSettings : DefaultCommandSettings {

    /// <summary>
    /// Final execute method that wraps the command execution with error handling.
    /// Override ExecuteCommand instead of this method.
    /// </summary>
    public override async Task<int> ExecuteAsync(CommandContext context, TSettings settings) {
        return await ErrorHandler.SafeExecuteAsync(async () => await ExecuteCommandAsync(context, settings), settings);
    }

    /// <summary>
    /// Override this method to implement your command logic.
    /// Any exceptions thrown will be automatically handled by the ErrorHandler.
    /// </summary>
    /// <param name="context">The command context</param>
    /// <param name="settings">The command settings</param>
    /// <returns>Exit code - 0 for success, negative values for errors</returns>
    protected abstract Task<int> ExecuteCommandAsync(CommandContext context, TSettings settings);
}
