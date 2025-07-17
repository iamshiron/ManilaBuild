using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Shiron.Manila.CLI.Exceptions;
using Shiron.Manila.Ext;
using Spectre.Console.Cli;

namespace Shiron.Manila.CLI.Commands;

public sealed class RunCommand : BaseAsyncManilaCommand<RunCommand.Settings> {
    public class Settings : DefaultCommandSettings {
        [CommandArgument(0, "<job>")]
        [Description("The job to run")]
        [Required]
        public string Job { get; set; } = "";
    }

    protected override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings) {
        ManilaCLI.SetupInitialComponents(settings);
        ManilaCLI.InitExtensions();

        var engine = ManilaEngine.GetInstance();
        var extensionManager = ExtensionManager.GetInstance();

        await ManilaCLI.StartEngine(engine);
        return engine.GetJob(settings.Job) == null
            ? throw new JobNotFoundException(settings.Job)
            : ManilaCLI.RunJob(engine, extensionManager, settings, settings.Job);
    }
}
