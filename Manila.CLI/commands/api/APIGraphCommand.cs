
using System.ComponentModel;
using Shiron.Manila.API;
using Shiron.Manila.Utils;
using Spectre.Console.Cli;
using static Shiron.Manila.CLI.CLIConstants;

namespace Shiron.Manila.CLI.Commands.API;

[Description("Retrieve the execution graph as a mermaid diagram.")]
public class APIGraphCommand(BaseServiceCotnainer baseServices, ServiceContainer? services = null, ManilaEngine? engine = null, Workspace? workspace = null) :
    BaseManilaCommand<APIGraphCommand.Settings>(baseServices.Logger) {

    private readonly BaseServiceCotnainer _baseServices = baseServices;
    private readonly ServiceContainer? _services = services;
    private readonly Workspace? _workspace = workspace;
    private readonly ManilaEngine? _engine = engine;

    public class Settings : APICommandSettings { }

    protected override int ExecuteCommand(CommandContext context, Settings settings) {
        if (_services == null || _workspace == null || _engine == null) {
            _baseServices.Logger.Error(Messages.ManilaEngineNotInitialized);
            return ExitCodes.USER_COMMAND_ERROR;
        }

        Console.WriteLine(_engine.CreateExecutionGraph(_services, _baseServices, _workspace).ToMermaid());

        return ExitCodes.SUCCESS;
    }
}
