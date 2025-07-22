using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Shiron.Manila.API;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Ext;
using Spectre.Console.Cli;

namespace Shiron.Manila.CLI.Commands;

[Description("Runs a job in the current workspace")]
internal sealed class RunCommand(ManilaEngine engine, ServiceContainer services, Workspace workspace) : BaseManilaCommand<RunCommand.Settings> {
    private readonly ManilaEngine _engine = engine;
    private readonly ServiceContainer _services = services;
    private readonly Workspace _workspace = workspace;

    public class Settings : DefaultCommandSettings {
        [CommandArgument(0, "<job>")]
        [Description("The job to run")]
        [Required]
        public string Job { get; set; } = "";
    }

    protected override int ExecuteCommand(CommandContext context, Settings settings) {
        var engine = _engine ?? throw new ManilaException("Manila engine is not initialized.");

        _services.Logger.Info($"Running job: {string.Join(",", _services.JobRegistry.JobKeys)}");

        return _services.JobRegistry.GetJob(settings.Job) == null
            ? throw new JobNotFoundException(settings.Job)
            : ManilaCLI.RunJob(engine, _workspace, settings, settings.Job);
    }
}
