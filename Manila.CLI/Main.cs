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
        try {
            engine.ExecuteBuildLogic(task);
        } catch (ScriptingException e) {
            //  scripting errors are common user-facing issues.
            AnsiConsole.MarkupLine($"\n[red]{Emoji.Known.CrossMark} Script Error:[/] [white]{Markup.Escape(e.Message)}[/]");
            AnsiConsole.MarkupLine("[grey]This error occurred while executing a script. Check the script for syntax errors or logic issues.[/]");
            AnsiConsole.MarkupLine("[grey]Run with --stack-trace for a detailed technical log.[/]");
            if (settings.StackTrace) Utils.TryWriteException(e.InnerException ?? e);

            return ExitCodes.SCRIPTING_ERROR;
        } catch (BuildException e) {
            // build errors indicate a failure in the compilation or packaging process.
            AnsiConsole.MarkupLine($"\n[red]{Emoji.Known.CrossMark} Build Error:[/] [white]{e.Message}[/]");
            AnsiConsole.MarkupLine("[grey]The project failed to build. Review the build configuration and source files for errors.[/]");
            AnsiConsole.MarkupLine("[grey]Run with --stack-trace for a detailed technical log.[/]");
            if (settings.StackTrace) Utils.TryWriteException(e.InnerException ?? e);

            return ExitCodes.BUILD_ERROR;
        } catch (ConfigurationException e) {
            // configuration errors are often due to invalid settings.
            AnsiConsole.MarkupLine($"\n[yellow]{Emoji.Known.Warning} Configuration Error:[/] [white]{e.Message}[/]");
            AnsiConsole.MarkupLine("[grey]There is a problem with a configuration file or setting. Please verify it is correct.[/]");
            AnsiConsole.MarkupLine("[grey]Run with --stack-trace for more technical details.[/]");
            if (settings.StackTrace) Utils.TryWriteException(e.InnerException ?? e);

            return ExitCodes.CONFIGURATION_ERROR;
        } catch (ManilaException e) {
            // this is a known, handled application error.
            AnsiConsole.MarkupLine($"\n[yellow]{Emoji.Known.Warning} Application Error:[/] [white]{e.Message}[/]");
            AnsiConsole.MarkupLine($"[grey]A known issue ('{e.GetType().Name}') occurred. This is a handled error condition.[/]");
            AnsiConsole.MarkupLine("[grey]Run with --stack-trace for more technical details.[/]");
            if (settings.StackTrace) Utils.TryWriteException(e);

            return ExitCodes.KNOWN_ERROR;
        } catch (Exception e) {
            // this is a critical, unexpected error that likely indicates a bug.
            AnsiConsole.MarkupLine($"\n[red]{Emoji.Known.Collision} Unexpected System Exception:[/] [white]{e.GetType().Name}[/]");
            AnsiConsole.MarkupLine("[red]This may indicate a bug in the application. Please report this issue.[/]");
            AnsiConsole.MarkupLine("[grey]Run with --stack-trace for a detailed error log.[/]");
            if (settings.StackTrace) Utils.TryWriteException(e);

            return ExitCodes.UNKNOWN_ERROR;
        }

        extensionManager.ReleasePlugins();
        return ExitCodes.SUCCESS;
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
        });

        var exitCode = CommandApp.Run(args);
        Profiler.SaveToFile(ProfilingDir);
        return exitCode;
    }
}
