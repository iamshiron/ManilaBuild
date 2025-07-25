

using System.ComponentModel;
using Shiron.Manila.API;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Logging;
using Shiron.Manila.Utils;
using Spectre.Console;
using Spectre.Console.Cli;
using static Shiron.Manila.CLI.CLIConstants;

namespace Shiron.Manila.CLI.Commands;

[Description("Creates a new project based on a template.")]
public class NewCommand(BaseServiceCotnainer baseServices, ServiceContainer? services = null) :
    BaseAsyncManilaCommand<NewCommand.Settings>(baseServices) {

    private readonly ServiceContainer? _services = services;
    private readonly BaseServiceCotnainer _baseServices = baseServices;

    public sealed class Settings : DefaultCommandSettings {
        [CommandArgument(0, "<name>")]
        public string Name { get; set; } = string.Empty;

        [CommandArgument(1, "<template>")]
        public string Template { get; set; } = string.Empty;
    }

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

