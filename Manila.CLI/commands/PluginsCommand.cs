using Shiron.Manila.Ext;
using Spectre.Console;
using Spectre.Console.Cli;
using static Shiron.Manila.CLI.CLIConstants;

namespace Shiron.Manila.CLI.Commands;

internal sealed class PluginsCommand : BaseManilaCommand<PluginsCommand.Settings> {
    public class Settings : DefaultCommandSettings { }

    protected override int ExecuteCommand(CommandContext context, Settings settings) {
        var extensionManager = ExtensionManager.GetInstance();
        ManilaCLI.InitExtensions();

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn(new TableColumn(TableColumns.Project)); // Using Project column for Plugin
        table.AddColumn(new TableColumn(TableColumns.Version));
        table.AddColumn(new TableColumn(TableColumns.Group));
        table.AddColumn(new TableColumn(TableColumns.Path));
        table.AddColumn(new TableColumn(TableColumns.Author));
        foreach (var p in extensionManager.Plugins) {
            table.AddRow(
                string.Format(Format.JobIdentifier, p.Name),
                p.Version.ToString(),
                p.Group ?? "",
                Path.GetFileName(p.File) ?? "",
                p.Authors.Count > 0 ? Markup.Escape($"{string.Join(", ", p.Authors)}") : "");
        }

        AnsiConsole.Write(new Rule(string.Format(Format.Rule, Messages.AvailablePlugins) + "\n").RuleStyle(BorderStyles.Default).DoubleBorder());
        AnsiConsole.Write(table);

        extensionManager.ReleasePlugins();

        return ExitCodes.SUCCESS;
    }
}
