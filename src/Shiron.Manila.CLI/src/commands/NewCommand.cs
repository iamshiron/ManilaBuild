

using System.ComponentModel;
using Shiron.Logging;
using Shiron.Manila.API;
using Shiron.Manila.API.Ext;
using Shiron.Manila.API.Utils;
using Shiron.Manila.Exceptions;
using Shiron.Utils;
using Spectre.Console;
using Spectre.Console.Cli;
using static Shiron.Manila.CLI.CLIConstants;

namespace Shiron.Manila.CLI.Commands;

/// <summary>
/// Creates a new project from a plugin template
/// </summary>
[Description("Create new project from template")]
public class NewCommand(BaseServiceContainer baseServices, ServiceContainer? services = null) :
    BaseAsyncManilaCommand<NewCommand.Settings>(baseServices) {

    private readonly ServiceContainer? _services = services;
    private readonly BaseServiceContainer _baseServices = baseServices;

    /// <summary>
    /// Command settings specifying name and template
    /// </summary>
    public sealed class Settings : DefaultCommandSettings {
        [CommandArgument(0, "<name>")]
        public string Name { get; set; } = string.Empty;

        [CommandArgument(1, "<template>")]
        public string Template { get; set; } = string.Empty;
    }

    /// <summary>
    /// Creates project using resolved template
    /// </summary>
    protected override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings) {
        if (_services == null) {
            _baseServices.Logger.Error(Messages.ManilaEngineNotInitialized);
            return ExitCodes.USER_COMMAND_ERROR;
        }

        var projectName = settings.Name;
        var templateMatch = RegexUtils.MatchTemplate(settings.Template)
            ?? throw new ManilaException("Invalid template format. Use 'plugin:template' format.");

        var plugin = _services.ExtensionManager.Plugins.FirstOrDefault(p => p.Name == templateMatch.Plugin)
            ?? throw new ManilaException($"Plugin '{templateMatch.Plugin}' not found.");

        var template = plugin.ProjectTemplates[templateMatch.Template]
            ?? throw new ManilaException($"Template '{templateMatch.Template}' not found in plugin '{plugin.Name}'.");

        var root = Path.Join(".", projectName);
        await ProjectCreator.Create(root, template, []);

        _baseServices.Logger.MarkupLine(Messages.ProjectCreatedSuccessfully);

        return ExitCodes.SUCCESS;
    }
}

