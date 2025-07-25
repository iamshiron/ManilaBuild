
using System.ComponentModel;
using Shiron.Manila.Logging;
using Shiron.Manila.Utils;
using Spectre.Console;
using Spectre.Console.Cli;
using static Shiron.Manila.CLI.CLIConstants;

namespace Shiron.Manila.CLI.Commands;

[Description("Lists available project templates.")]
public class TemplatesCommand(BaseServiceCotnainer baseServices, ServiceContainer? services = null) :
    BaseManilaCommand<TemplatesCommand.Settings>(baseServices.Logger) {

    private readonly ServiceContainer? _services = services;
    private readonly BaseServiceCotnainer _baseServices = baseServices;

    public sealed class Settings : DefaultCommandSettings { }

    protected override int ExecuteCommand(CommandContext context, Settings settings) {
        if (_services == null) {
            _baseServices.Logger.Error(Messages.ManilaEngineNotInitialized);
            return ExitCodes.USER_COMMAND_ERROR;
        }

        var table = new Table().Border(TableBorder.Rounded)
            .AddColumn(TableColumns.Template)
            .AddColumn(TableColumns.Description)
            .AddColumn(TableColumns.Plugin);

        var mgr = _services.ExtensionManager;
        foreach (var plugin in mgr.Plugins) {
            foreach (var (_, template) in plugin.ProjectTemplates) {
                _ = table.AddRow(
                    template.Name,
                    template.Description ?? "[grey](none)[/]",
                    $"{plugin.Group}:{plugin.Name}"
                );
            }
        }

        AnsiConsole.Write(table);

        return ExitCodes.SUCCESS;
    }
}
