using Shiron.Manila;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Ext;
using Shiron.Manila.CLI;
using Shiron.Manila.CLI.Commands;
using Spectre.Console;
using Spectre.Console.Cli;
using Shiron.Manila.Profiling;
using Shiron.Manila.CLI.Exceptions;
using Shiron.Manila.Utils;
using Shiron.Manila.API;
using Shiron.Manila.Artifacts;

namespace Shiron.Manila.CLI;

public static class ManilaCLI {
    public static string PluginDir => Path.Combine(Directory.GetCurrentDirectory(), ".manila", "plugins");
    public static string NugetDir => Path.Combine(Directory.GetCurrentDirectory(), ".manila", "nuget");
    public static string ProfilingDir => Path.Combine(Directory.GetCurrentDirectory(), ".manila", "profiles");
    public static CommandApp<DefaultCommand> CommandApp = new CommandApp<DefaultCommand>();

    public static void SetupInitialComponents(DefaultCommandSettings settings) {
        AnsiConsoleRenderer.Init(settings.Quiet, settings.Verbose, settings.Structured, settings.StackTrace);
    }

    public static void InitExtensions() {
        using (new ProfileScope("Initializing Plugins")) {
            var extensionManager = ExtensionManager.GetInstance();
            extensionManager.Init("./.manila/plugins");
            extensionManager.LoadPlugins();
            extensionManager.InitPlugins();
        }
    }

    public static async System.Threading.Tasks.Task StartEngine(ManilaEngine engine) {
        await engine.Run();
        if (engine.Workspace == null) throw new Exception("Workspace not found!");
    }

    public static int RunTask(ManilaEngine engine, ExtensionManager extensionManager, DefaultCommandSettings settings, string task) {
        return ErrorHandler.SafeExecute(() => {
            engine.ExecuteBuildLogic(task);
            extensionManager.ReleasePlugins();
            return ExitCodes.SUCCESS;
        }, settings);
    }

    public static int Main(string[] args) {
#if DEBUG
        Directory.SetCurrentDirectory("E:\\dev\\Manila\\manila\\run");
        Profiler.IsEnabled = true;
#endif

        var logOptions = new {
            Structured = args.Contains("--structured") || args.Contains("--json"),
            Verbose = args.Contains("--verbose"),
            Quiet = args.Contains("--quiet"),
            StackTrace = args.Contains("--stack-trace")
        };

        if (!(args.Contains("--quiet") || args.Contains("-q"))) {
            AnsiConsole.MarkupLine(@"[blue] __  __             _ _[/]");
            AnsiConsole.MarkupLine(@"[blue]|  \/  | __ _ _ __ (_| | __ _[/]");
            AnsiConsole.MarkupLine(@"[blue]| |\/| |/ _` | '_ \| | |/ _` |[/]");
            AnsiConsole.MarkupLine(@"[blue]| |  | | (_| | | | | | | (_| |[/]");
            AnsiConsole.MarkupLine($"[blue]|_|  |_|\\__,_|_| |_|_|_|\\__,_|[/] [magenta]v{ManilaEngine.VERSION}[/]\n");
        }

        CommandApp.Configure(c => {
            c.SetApplicationName("manila");
            c.SetApplicationVersion(ManilaEngine.VERSION);
            c.AddCommand<PluginsCommand>("plugins");
            c.AddCommand<TasksCommand>("tasks");
            c.AddCommand<RunCommand>("run");
            c.AddCommand<ArtifactsCommand>("artifacts");
            c.AddCommand<ProjectsCommand>("projects");
            c.AddCommand<ApiCommand>("api");
        });

        var exitCode = CommandApp.Run(args);
        Profiler.SaveToFile(ProfilingDir);
        return exitCode;
    }
}
