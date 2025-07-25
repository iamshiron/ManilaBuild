using System.ComponentModel;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Ext;
using Spectre.Console;
using Spectre.Console.Cli;
using static Shiron.Manila.CLI.CLIConstants;

namespace Shiron.Manila.CLI.Commands;

[Description("List all available plugins in the current workspace")]
internal sealed class PluginsCommand(BaseServiceCotnainer baseServices, ServiceContainer? services = null) :
    BaseManilaCommand<PluginsCommand.Settings>(baseServices) {

    private readonly ServiceContainer? _services = services;
    private readonly BaseServiceCotnainer _baseServices = baseServices;

    public class Settings : DefaultCommandSettings { }

    protected override int ExecuteCommand(CommandContext context, Settings settings) {
        if (_services == null) {
            _baseServices.Logger.Error(Messages.ManilaEngineNotInitialized);
            return ExitCodes.USER_COMMAND_ERROR;
        }

        var table = new Table().Border(TableBorder.Rounded)
            .AddColumn(new TableColumn(TableColumns.Plugin))
            .AddColumn(new TableColumn(TableColumns.Version))
            .AddColumn(new TableColumn(TableColumns.Group))
            .AddColumn(new TableColumn(TableColumns.Path))
            .AddColumn(new TableColumn(TableColumns.Author));

        foreach (var p in _services.ExtensionManager.Plugins) {
            _ = table.AddRow(
                string.Format(Format.JobIdentifier, p.Name),
                p.Version.ToString(),
                p.Group ?? "",
                Path.GetFileName(p.File) ?? "",
                p.Authors.Count > 0 ? Markup.Escape($"{string.Join(", ", p.Authors)}") : "");
        }

        AnsiConsole.Write(new Rule(string.Format(Format.Rule, Messages.AvailablePlugins) + "\n").RuleStyle(BorderStyles.Default).DoubleBorder());
        AnsiConsole.Write(table);

        return ExitCodes.SUCCESS;
    }
}
