using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Shiron.Manila.CLI.Exceptions;
using Shiron.Manila.Ext;
using Spectre.Console.Cli;

namespace Shiron.Manila.CLI.Commands;

public sealed class RunCommand : BaseAsyncManilaCommand<RunCommand.Settings> {
    public class Settings : DefaultCommandSettings {
        [CommandArgument(0, "<task>")]
        [Description("The task to run")]
        [Required]
        public string Task { get; set; } = "";
    }

    protected override async System.Threading.Tasks.Task<int> ExecuteCommandAsync(CommandContext context, Settings settings) {
        ManilaCLI.SetupInitialComponents(settings);
        ManilaCLI.InitExtensions();

        var engine = ManilaEngine.GetInstance();
        var extensionManager = ExtensionManager.GetInstance();

        await ManilaCLI.StartEngine(engine);
        if (engine.GetTask(settings.Task) == null) {
            throw new TaskNotFoundException(settings.Task);
        }

        return ManilaCLI.RunTask(engine, extensionManager, settings, settings.Task);
    }
}
