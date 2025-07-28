
using Shiron.Manila.CLI.Commands;
using Shiron.Manila.Logging;
using Spectre.Console.Cli;

namespace Shiron.Manila.CLI;

/// <summary>
/// Base class for all Manila CLI commands that provides centralized error handling.
/// </summary>
/// <typeparam name="TSettings">The settings type for the command</typeparam>
public abstract class BaseManilaCommand<TSettings>(BaseServiceContainer baseServers) : Command<TSettings>
    where TSettings : DefaultCommandSettings {

    private readonly ILogger _logger = baseServers.Logger;

    /// <summary>
    /// Final execute method that wraps the command execution with error handling.
    /// Override ExecuteCommand instead of this method.
    /// </summary>
    public sealed override int Execute(CommandContext context, TSettings settings) {
        return ErrorHandler.SafeExecute(_logger, () => ExecuteCommand(context, settings), settings.ToLogOptions());
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
public abstract class BaseAsyncManilaCommand<TSettings>(BaseServiceContainer container) : AsyncCommand<TSettings>
    where TSettings : DefaultCommandSettings {

    private readonly ILogger _logger = container.Logger;

    /// <summary>
    /// Final execute method that wraps the command execution with error handling.
    /// Override ExecuteCommand instead of this method.
    /// </summary>
    public override async Task<int> ExecuteAsync(CommandContext context, TSettings settings) {
        return await ErrorHandler.SafeExecuteAsync(_logger, () => ExecuteCommandAsync(context, settings), settings.ToLogOptions());
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
