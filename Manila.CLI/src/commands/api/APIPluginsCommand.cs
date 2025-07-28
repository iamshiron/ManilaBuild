
using System.ComponentModel;
using Newtonsoft.Json;
using Shiron.Manila.API;
using Shiron.Manila.API.Interfaces;
using Shiron.Manila.Ext;
using Spectre.Console.Cli;
using static Shiron.Manila.CLI.CLIConstants;

namespace Shiron.Manila.CLI.Commands.API;

[Description("Retrieve plugins information")]
internal sealed class APIPluginsCommand(BaseServiceContainer baseServices, ServiceContainer? services = null) :
    BaseManilaCommand<APIPluginsCommand.Settings>(baseServices) {

    private readonly BaseServiceContainer _baseServices = baseServices;
    private readonly ServiceContainer? _services = services;

    public class Settings : APICommandSettings {
        [Description("Include detailed information")]
        [CommandOption("--detailed")]
        public bool Detailed { get; set; } = false;

        [Description("Output in compact format")]
        [CommandOption("--no-indent")]
        public bool NoIndent { get; set; } = false;

        [Description("No null values in output")]
        [CommandOption("--no-null-values")]
        public bool NoNullValues { get; set; } = false;

        [Description("Include default values in output")]
        [CommandOption("--include-default-values")]
        public bool IncludeDefaultValues { get; set; } = false;
    }

    protected override int ExecuteCommand(CommandContext context, Settings settings) {
        if (_services == null) {
            _baseServices.Logger.Error(Messages.ManilaEngineNotInitialized);
            return ExitCodes.USER_COMMAND_ERROR;
        }

        Console.WriteLine(APICommandHelpers.FormatData(
            GetData(_services.ExtensionManager, settings),
            settings.NoIndent, settings.NoNullValues, settings.IncludeDefaultValues
        ));

        return ExitCodes.SUCCESS;
    }

    private static object GetData(IExtensionManager mgr, Settings settings) {
        var list = new List<object>();

        foreach (var plugin in mgr.Plugins) {
            var pluginData = new {
                group = plugin.Group,
                name = plugin.Name,
                version = plugin.Version
            };
            if (settings.Detailed) {
                list.Add(new {
                    pluginData.group,
                    pluginData.name,
                    pluginData.version,
                    components = plugin.Components.Keys.ToArray(),
                    apiClasses = plugin.APIClasses.Keys.ToArray()
                });
            } else {
                list.Add(pluginData);
            }
        }
        return new {
            plugins = list.ToArray(),
            count = list.Count
        };
    }
}
