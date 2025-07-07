
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using Shiron.Manila;
using Shiron.Manila.CLI;
using Shiron.Manila.CLI.Commands;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Ext;
using Spectre.Console.Cli;

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

        var task = ManilaCLI.RunTask(engine, extensionManager, settings, settings.Task);
        task.Wait();
        return task.Result;
    }
}
