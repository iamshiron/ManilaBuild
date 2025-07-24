
using System.ComponentModel;
using Shiron.Manila.API;
using Shiron.Manila.Utils;
using Spectre.Console.Cli;
using static Shiron.Manila.CLI.CLIConstants;

namespace Shiron.Manila.CLI.Commands.API;

[Description("Retrieve the execution graph layers for a specific job in json format.")]
public class APIBuildLayersCommand(BaseServiceCotnainer baseServices, ServiceContainer? services = null, ManilaEngine? engine = null, Workspace? workspace = null) : BaseManilaCommand<APIBuildLayersCommand.Settings> {
    private readonly BaseServiceCotnainer _baseServices = baseServices;
    private readonly ServiceContainer? _services = services;
    private readonly Workspace? _workspace = workspace;
    private readonly ManilaEngine? _engine = engine;

    public class Settings : APICommandSettings {
        [CommandArgument(0, "<job>")]
        [Description("The job to create the graph for.")]
        public string Job { get; set; } = string.Empty;
    }

    protected override int ExecuteCommand(CommandContext context, Settings settings) {
        if (_services == null || _workspace == null || _engine == null) {
            _baseServices.Logger.Error(Messages.ManilaEngineNotInitialized);
            return ExitCodes.USER_COMMAND_ERROR;
        }

        if (settings.Job == null) {
            _baseServices.Logger.Error("No job specified. Please provide a job name.");
            return ExitCodes.USER_COMMAND_ERROR;
        }

        Console.WriteLine(APICommandHelpers.FormatData(
            GetData(_engine.CreateExecutionGraph(_services, _baseServices, _workspace), settings.Job),
            settings
        ));


        return ExitCodes.SUCCESS;
    }

    public static object GetData(ExecutionGraph graph, string job) {
        var layers = graph.GetExecutionLayers(job);
        return new {
            job,
            layers = layers.Select(layer =>
                layer.Items.Select(obj => new {
                    type = obj.GetType().FullName,
                    hashCode = obj.GetHashCode().ToString("X"),
                    data = obj
                }).ToArray()
            ).ToArray()
        };
    }
}
