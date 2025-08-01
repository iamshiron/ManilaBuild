
using System.ComponentModel;
using Shiron.Manila.Utils;
using Spectre.Console;
using Spectre.Console.Cli;
using static Shiron.Manila.CLI.CLIConstants;

namespace Shiron.Manila.CLI.Commands;

[Description("Initializes a new Manila workspace")]
internal sealed class InitCommand(IDirectories directories, BaseServiceContainer baseServices) :
    BaseManilaCommand<InitCommand.Settings>(baseServices) {

    private readonly IDirectories _directories = directories;
    private readonly BaseServiceContainer _baseServices = baseServices;

    public sealed class Settings : DefaultCommandSettings {
        [CommandOption("--force")]
        [Description("Forces the initialization, overwriting existing files if necessary.")]
        [DefaultValue(false)]
        public bool Force { get; set; }
    }

    protected override int ExecuteCommand(CommandContext context, Settings settings) {
        var workspaceFound = Directory.Exists(_directories.Data) || File.Exists(Path.Join(_directories.Root, "Manila.cs"));

        if (workspaceFound && !settings.Force) {
            _baseServices.Logger.Error(Messages.AlreadyInitialized);
            return ExitCodes.USER_COMMAND_ERROR;
        }

        if (workspaceFound && settings.Force) {
            _baseServices.Logger.Info("Deleting old data dir...");
            Directory.Delete(_directories.Data, true);
        }

        _baseServices.Logger.Info(Messages.CreatingNewWorkspace);
        foreach (var dir in _directories.AllDataDirectories) {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            _baseServices.Logger.Debug($"Creating directory: {dir}");
            _ = Directory.CreateDirectory(dir);
        }

        // Create workspace file
        File.WriteAllLines(Path.Join(_directories.Root, "Manila.cs"), ScriptDefaults.WorkspaceScript);

        _baseServices.Logger.Info(Messages.WorkspaceInitialized);

        return ExitCodes.SUCCESS;
    }
}
