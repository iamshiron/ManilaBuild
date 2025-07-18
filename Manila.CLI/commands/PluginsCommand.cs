using System.ComponentModel;
using Shiron.Manila.Ext;
using Spectre.Console;
using Spectre.Console.Cli;
using static Shiron.Manila.CLI.CLIConstants;

namespace Shiron.Manila.CLI.Commands;

[Description("List all available plugins in the current workspace")]
internal sealed class PluginsCommand : BaseAsyncManilaCommand<PluginsCommand.Settings> {
    public class Settings : DefaultCommandSettings { }

    protected override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings) {
        var extensionManager = ExtensionManager.GetInstance();
        await ManilaCLI.InitExtensions();

        var table = new Table().Border(TableBorder.Rounded)
            .AddColumn(new TableColumn(TableColumns.Project))
            .AddColumn(new TableColumn(TableColumns.Version))
            .AddColumn(new TableColumn(TableColumns.Group))
            .AddColumn(new TableColumn(TableColumns.Path))
            .AddColumn(new TableColumn(TableColumns.Author));

        foreach (var p in extensionManager.Plugins) {
            _ = table.AddRow(
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
