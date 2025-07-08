using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Shiron.Manila.CLI.Exceptions;
using Shiron.Manila.Ext;
using Spectre.Console.Cli;

namespace Shiron.Manila.CLI.Commands;

public sealed class RunCommand : Command<RunCommand.Settings> {
    public class Settings : DefaultCommandSettings {
        [CommandArgument(0, "<task>")]
        [Description("The task to run")]
        [Required]
        public string Task { get; set; } = "";
    }

    public override int Execute(CommandContext context, Settings settings) {
        ManilaCLI.SetupInitialComponents(settings);
        ManilaCLI.InitExtensions();

        var engine = ManilaEngine.GetInstance();
        var extensionManager = ExtensionManager.GetInstance();

        ManilaCLI.StartEngine(engine).Wait();
        if (!engine.HasTask(settings.Task[1..])) throw new TaskNotFoundException(settings.Task);

        return ManilaCLI.RunTask(engine, extensionManager, settings, settings.Task);
    }
}
