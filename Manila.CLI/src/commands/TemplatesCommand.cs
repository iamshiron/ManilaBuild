
using System.ComponentModel;
using Shiron.Manila.Logging;
using Shiron.Manila.Utils;
using Spectre.Console;
using Spectre.Console.Cli;
using static Shiron.Manila.CLI.CLIConstants;

namespace Shiron.Manila.CLI.Commands;

/// <summary>
/// Lists project templates exposed by plugins
/// </summary>
[Description("List available project templates")]
public class TemplatesCommand(BaseServiceContainer baseServices, ServiceContainer? services = null) :
    BaseManilaCommand<TemplatesCommand.Settings>(baseServices) {

    private readonly ServiceContainer? _services = services;
    private readonly BaseServiceContainer _baseServices = baseServices;

    /// <summary>
    /// Command settings (no additional options)
    /// </summary>
    public sealed class Settings : DefaultCommandSettings { }

    /// <summary>
    /// Renders template listing table
    /// </summary>
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
