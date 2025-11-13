using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Shiron.Manila.API;
using Shiron.Manila.Exceptions;
using Spectre.Console.Cli;
using static Shiron.Manila.CLI.CLIConstants;

namespace Shiron.Manila.CLI.Commands;

/// <summary>
/// Runs a single job in current workspace
/// </summary>
[Description("Runs a job in the current workspace")]
internal sealed class RunCommand(BaseServiceContainer baseServices, ManilaEngine? engine = null, ServiceContainer? services = null, Workspace? workspace = null) :
    BaseAsyncManilaCommand<RunCommand.Settings>(baseServices) {

    private readonly ManilaEngine? _engine = engine;
    private readonly ServiceContainer? _services = services;
    private readonly Workspace? _workspace = workspace;
    private readonly BaseServiceContainer _baseServices = baseServices;

    /// <summary>
    /// Command settings for job execution
    /// </summary>
    public class Settings : DefaultCommandSettings {
        /// <summary>
        /// Target job identifier
        /// </summary>
        [CommandArgument(0, "<job>")]
        [Description("The job to run")]
        [Required]
        public string Job { get; set; } = "";
    }

    /// <summary>
    /// Executes job with provided identifier
    /// </summary>
    protected override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings) {
        if (_engine == null || _services == null || _workspace == null) {
            _baseServices.Logger.Error(Messages.ManilaEngineNotInitialized);
            return ExitCodes.USER_COMMAND_ERROR;
        }

        var engine = _engine ?? throw new ManilaException("Manila engine is not initialized.");

        return _services.JobRegistry.GetJob(settings.Job) == null
            ? throw new ManilaException($"Job '{settings.Job}' not found.")
            : await ManilaCli.RunJobAsync(_services, _baseServices, engine, _workspace, settings, settings.Job);
    }
}
