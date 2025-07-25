using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Shiron.Manila.API;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Ext;
using Spectre.Console.Cli;
using static Shiron.Manila.CLI.CLIConstants;

namespace Shiron.Manila.CLI.Commands;

[Description("Runs a job in the current workspace")]
internal sealed class RunCommand(BaseServiceCotnainer baseServices, ManilaEngine? engine = null, ServiceContainer? services = null, Workspace? workspace = null) :
    BaseAsyncManilaCommand<RunCommand.Settings>(baseServices) {

    private readonly ManilaEngine? _engine = engine;
    private readonly ServiceContainer? _services = services;
    private readonly Workspace? _workspace = workspace;
    private readonly BaseServiceCotnainer _baseServices = baseServices;

    public class Settings : DefaultCommandSettings {
        [CommandArgument(0, "<job>")]
        [Description("The job to run")]
        [Required]
        public string Job { get; set; } = "";
    }

    protected override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings) {
        if (_engine == null || _services == null || _workspace == null) {
            _baseServices.Logger.Error(Messages.ManilaEngineNotInitialized);
            return ExitCodes.USER_COMMAND_ERROR;
        }

        var safeEngine = _engine ?? throw new ManilaException("Manila engine is not initialized.");
        var safeServices = _services ?? throw new ManilaException("Services are not initialized.");

        return _services.JobRegistry.GetJob(settings.Job) == null
            ? throw new ManilaException(settings.Job)
            : await ManilaCli.RunJobAsync(safeServices, _baseServices, safeEngine, _workspace, settings, settings.Job);
    }
}
