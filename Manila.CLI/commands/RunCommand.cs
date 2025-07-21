using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Ext;
using Spectre.Console.Cli;

namespace Shiron.Manila.CLI.Commands;

[Description("Runs a job in the current workspace")]
internal sealed class RunCommand : BaseAsyncManilaCommand<RunCommand.Settings> {
    public class Settings : DefaultCommandSettings {
        [CommandArgument(0, "<job>")]
        [Description("The job to run")]
        [Required]
        public string Job { get; set; } = "";
    }

    protected override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings) {
        if (ManilaCLI.Profiler == null || ManilaCLI.ManilaEngine == null || ManilaCLI.Logger == null)
            throw new ManilaException("Manila engine, profiler, or logger is not initialized.");

        ManilaCLI.SetupInitialComponents(ManilaCLI.Logger, settings);

        await ManilaCLI.InitExtensions(ManilaCLI.Profiler, ManilaCLI.ManilaEngine ?? throw new ManilaException("Manila engine is not initialized."));

        var engine = ManilaCLI.ManilaEngine ?? throw new ManilaException("Manila engine is not initialized.");
        await ManilaCLI.StartEngine(engine);
        return engine.JobRegisry.GetJob(settings.Job) == null
            ? throw new JobNotFoundException(settings.Job)
            : ManilaCLI.RunJob(engine, engine.ExtensionManager, settings, settings.Job);
    }
}
