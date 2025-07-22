using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ClearScript.V8;
using Microsoft.Extensions.DependencyInjection;
using NuGet.Packaging.Signing;
using Shiron.Manila;
using Shiron.Manila.API;
using Shiron.Manila.API.Bridges;
using Shiron.Manila.API.Builders;
using Shiron.Manila.Artifacts;
using Shiron.Manila.Caching;
using Shiron.Manila.CLI;
using Shiron.Manila.CLI.Commands;
using Shiron.Manila.CLI.Utils;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Ext;
using Shiron.Manila.Logging;
using Shiron.Manila.Profiling;
using Shiron.Manila.Registries;
using Shiron.Manila.Utils;
using Spectre.Console;
using Spectre.Console.Cli;
using static Shiron.Manila.CLI.CLIConstants;

namespace Shiron.Manila.CLI;

public static class ManilaCLI {
    public static CommandApp<DefaultCommand>? CommandApp { get; private set; }
    public static IDirectories? Directories { get; private set; }

    public static void SetupBaseComponents(ILogger logger, LogOptions logOptions) {
        AnsiConsoleRenderer.Init(logger, logOptions);
    }

    private static async Task InitExtensions(ServiceContainer services) {
        if (Directories == null) {
            throw new InvalidOperationException("Directories are not initialized.");
        }

        using (new ProfileScope(services.Profiler, "Initializing Plugins")) {
            await services.ExtensionManager.LoadPlugins();
            services.ExtensionManager.InitPlugins();
        }
    }

    private static V8ScriptEngine CreateScriptEngine() {
        return new(
            V8ScriptEngineFlags.EnableTaskPromiseConversion
        ) {
            ExposeHostObjectStaticMembers = true,
        };
    }

    public static int RunJob(ManilaEngine engine, Workspace workspace, DefaultCommandSettings settings, string job) {
        var graph = engine.CreateExecutionGraph(workspace);

        return ErrorHandler.SafeExecute(() => {
            engine.ExecuteBuildLogic(graph, job);
            return ExitCodes.SUCCESS;
        }, settings);
    }

    public static async Task<int> Main(string[] args) {
#if DEBUG
        Directory.SetCurrentDirectory(Path.Join(AppDomain.CurrentDomain.BaseDirectory, "../../../../run"));
#endif
        Console.OutputEncoding = Encoding.UTF8;

        LogOptions logOptions = new LogOptions(
            args.Contains(CommandOptions.Quiet) || args.Contains(CommandOptions.QuietShort),
            args.Contains(CommandOptions.Verbose),
            args.Contains(CommandOptions.Structured) || args.Contains(CommandOptions.Json),
            args.Contains(CommandOptions.StackTrace)
        );

        Directories = new Directories();

        var logger = new Logger(null);
        var profiler = new Profiler(logger);
        var nugetManager = new NuGetManager(logger, profiler, Directories.Nuget);
        var services = new ServiceCollection();

        var serviceContainer = new ServiceContainer(
            logger,
            profiler,
            new JobRegistry(),
            new ArtifactManager(logger, profiler, Directories.Artifacts, Path.Join(Directories.Cache, "artifacts.json")),
            new ExtensionManager(logger, profiler, Directories.Plugins, nugetManager),
            nugetManager,
            new FileHashCache(Path.Join(Directories.DataDir, "cache", "filehashes.db"), Directories.RootDir)
        );

        await serviceContainer.ArtifactManager.LoadCache();

        var manilaEngine = new ManilaEngine(serviceContainer, Directories);
        SetupBaseComponents(logger, logOptions);

        if (!(args.Contains(CommandOptions.Quiet) || args.Contains(CommandOptions.QuietShort))) {
            foreach (string line in Banner.Lines.Take(Banner.Lines.Length - 1)) {
                AnsiConsole.MarkupLine(line);
            }
            AnsiConsole.MarkupLine(string.Format(Banner.Lines.Last(), ManilaEngine.VERSION) + "\n");
        }

        await InitExtensions(serviceContainer);

        // Run engine and initialize projects
        var workspace = await manilaEngine.RunWorkspaceScript(new(
            serviceContainer.Logger, serviceContainer.Profiler,
            CreateScriptEngine(),
            Directories.RootDir, Path.Join(Directories.RootDir, "Manila.js")
        ));

        var workspaceBridge = new WorkspaceScriptBridge(serviceContainer.Logger, serviceContainer.Profiler, workspace);

        foreach (var script in manilaEngine.DiscoverProjectScripts()) {
            var projet = await manilaEngine.RunProjectScript(new(
                serviceContainer.Logger, serviceContainer.Profiler,
                CreateScriptEngine(),
                Directories.RootDir, script
            ), workspace, workspaceBridge);
        }

        _ = services.AddSingleton(serviceContainer)
            .AddSingleton(manilaEngine)
            .AddSingleton(workspace);
        _ = services.AddTransient<RunCommand>()
            .AddTransient<PluginsCommand>()
            .AddTransient<JobsCommand>()
            .AddTransient<ArtifactsCommand>()
            .AddTransient<ProjectsCommand>()
            .AddTransient<ApiCommand>();

        CommandApp = new CommandApp<DefaultCommand>(new TypeRegistrar(services));

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
        serviceContainer.ExtensionManager.ReleasePlugins();

        profiler.SaveToFile(Directories.Profiles);
        manilaEngine.Dispose();

        return exitCode;
    }
}
