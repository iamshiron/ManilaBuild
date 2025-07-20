using System.Text;
using Shiron.Manila;
using Shiron.Manila.API;
using Shiron.Manila.Artifacts;
using Shiron.Manila.CLI;
using Shiron.Manila.CLI.Commands;
using Shiron.Manila.CLI.Exceptions;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Ext;
using Shiron.Manila.Profiling;
using Shiron.Manila.Utils;
using Spectre.Console;
using Spectre.Console.Cli;
using static Shiron.Manila.CLI.CLIConstants;

namespace Shiron.Manila.CLI;

public static class ManilaCLI {
    public static string PluginDir => Path.Combine(Directory.GetCurrentDirectory(), Directories.Plugins);
    public static string NugetDir => Path.Combine(Directory.GetCurrentDirectory(), Directories.Nuget);
    public static string ProfilingDir => Path.Combine(Directory.GetCurrentDirectory(), Directories.Profiles);
    public static CommandApp<DefaultCommand> CommandApp = new CommandApp<DefaultCommand>();

    public static void SetupInitialComponents(DefaultCommandSettings settings) {
        AnsiConsoleRenderer.Init(settings.Quiet, settings.Verbose, settings.Structured, settings.StackTrace);
    }

    public static async Task InitExtensions() {
        using (new ProfileScope("Initializing Plugins")) {
            var extensionManager = ExtensionManager.GetInstance();
            extensionManager.Init($"./{Directories.Plugins}");
            await extensionManager.LoadPlugins();
            extensionManager.InitPlugins();
        }
    }

    public static async Task StartEngine(ManilaEngine engine) {
        await engine.Run();
        if (engine.Workspace == null) throw new Exception("Workspace not found!");
    }

    public static int RunJob(ManilaEngine engine, ExtensionManager extensionManager, DefaultCommandSettings settings, string job) {
        return ErrorHandler.SafeExecute(() => {
            engine.ExecuteBuildLogic(job);
            extensionManager.ReleasePlugins();
            return ExitCodes.SUCCESS;
        }, settings);
    }

    public static int Main(string[] args) {
#if DEBUG
        Directory.SetCurrentDirectory(Path.Join(AppDomain.CurrentDomain.BaseDirectory, "../../../../run"));
        Profiler.IsEnabled = true;
#endif

        Console.OutputEncoding = Encoding.UTF8;

        var logOptions = new {
            Structured = args.Contains(CommandOptions.Structured) || args.Contains(CommandOptions.Json),
            Verbose = args.Contains(CommandOptions.Verbose),
            Quiet = args.Contains(CommandOptions.Quiet),
            StackTrace = args.Contains(CommandOptions.StackTrace)
        };

        if (!(args.Contains(CommandOptions.Quiet) || args.Contains(CommandOptions.QuietShort))) {
            foreach (string line in Banner.Lines.Take(Banner.Lines.Length - 1)) {
                AnsiConsole.MarkupLine(line);
            }
            AnsiConsole.MarkupLine(string.Format(Banner.Lines.Last(), ManilaEngine.VERSION) + "\n");
        }

        CommandApp.Configure(c => {
            c.SetApplicationName("manila");
            c.SetApplicationVersion(ManilaEngine.VERSION);
            c.AddCommand<PluginsCommand>("plugins");
            c.AddCommand<JobsCommand>("jobs");
            c.AddCommand<RunCommand>("run");
            c.AddCommand<ArtifactsCommand>("artifacts");
            c.AddCommand<ProjectsCommand>("projects");
            c.AddCommand<ApiCommand>("api");
        });

        var exitCode = CommandApp.Run(args);
        Profiler.SaveToFile(ProfilingDir);
        ManilaEngine.GetInstance().Dispose();

        return exitCode;
    }
}
