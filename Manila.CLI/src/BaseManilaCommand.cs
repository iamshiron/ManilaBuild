
using Shiron.Manila.CLI.Commands;
using Shiron.Manila.Logging;
using Spectre.Console.Cli;

namespace Shiron.Manila.CLI;

/// <summary>
/// Base CLI command with unified error handling
/// </summary>
/// <typeparam name="TSettings">Settings type</typeparam>
public abstract class BaseManilaCommand<TSettings>(BaseServiceContainer baseServers) : Command<TSettings>
    where TSettings : DefaultCommandSettings {

    private readonly ILogger _logger = baseServers.Logger;

    /// <summary>
    /// Sealed execution wrapper with error handling
    /// </summary>
    public sealed override int Execute(CommandContext context, TSettings settings) {
        return ErrorHandler.SafeExecute(_logger, () => ExecuteCommand(context, settings), settings.ToLogOptions());
    }

    /// <summary>
    /// Command logic implementation point
    /// </summary>
    /// <returns>Exit code</returns>
    protected abstract int ExecuteCommand(CommandContext context, TSettings settings);
}

/// <summary>
/// Async CLI command with unified error handling
/// </summary>
/// <typeparam name="TSettings">Settings type</typeparam>
public abstract class BaseAsyncManilaCommand<TSettings>(BaseServiceContainer container) : AsyncCommand<TSettings>
    where TSettings : DefaultCommandSettings {

    private readonly ILogger _logger = container.Logger;

    /// <summary>
    /// Execution wrapper with async error handling
    /// </summary>
    public override async Task<int> ExecuteAsync(CommandContext context, TSettings settings) {
        return await ErrorHandler.SafeExecuteAsync(_logger, () => ExecuteCommandAsync(context, settings), settings.ToLogOptions());
    }

    /// <summary>
    /// Async command logic implementation point
    /// </summary>
    /// <returns>Exit code</returns>
    protected abstract Task<int> ExecuteCommandAsync(CommandContext context, TSettings settings);
}
