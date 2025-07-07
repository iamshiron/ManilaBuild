
using Shiron.Manila.Ext;
using Shiron.Manila.Profiling;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shiron.Manila.CLI.Commands;

internal sealed class PluginsCommand : Command<PluginsCommand.Settings> {
    public class Settings : DefaultCommandSettings { }

    public override int Execute(CommandContext context, Settings settings) {
        var extensionManager = ExtensionManager.GetInstance();
        ManilaCLI.InitExtensions();

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn(new TableColumn("[cyan]Plugin[/]"));
        table.AddColumn(new TableColumn("[green]Version[/]"));
        table.AddColumn(new TableColumn("[magenta]Group[/]"));
        table.AddColumn(new TableColumn("[yellow]Path[/]"));
        table.AddColumn(new TableColumn("[red]Author[/]"));
        foreach (var p in extensionManager.Plugins) {
            table.AddRow(
                $"[bold cyan]{p.Name}[/]",
                p.Version.ToString(),
                p.Group ?? "",
                Path.GetFileName(p.File) ?? "",
                p.Authors.Count > 0 ? Markup.Escape($"{string.Join(", ", p.Authors)}") : "");
        }

        AnsiConsole.Write(new Rule("[bold yellow]Available Plugins[/]\n").RuleStyle("grey").DoubleBorder());
        AnsiConsole.Write(table);

        extensionManager.ReleasePlugins();

        return 0;
    }
}
