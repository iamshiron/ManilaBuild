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
using Shiron.Manila.CLI.Commands.API;
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
    public static readonly long ProgramStartedTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public static void SetupBaseComponents(ILogger logger, LogOptions logOptions) {
        AnsiConsoleRenderer.Init(logger, logOptions);
    }

    private static async Task InitExtensions(BaseServiceCotnainer baseServices, ServiceContainer services) {
        if (Directories == null) {
            throw new InvalidOperationException("Directories are not initialized.");
        }

        using (new ProfileScope(baseServices.Profiler, "Initializing Plugins")) {
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

    public static int RunJob(ServiceContainer services, ManilaEngine engine, Workspace workspace, DefaultCommandSettings settings, string job) {
        var graph = engine.CreateExecutionGraph(services, workspace);

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
        var services = new ServiceCollection();

        var baseServiceContainer = new BaseServiceCotnainer(
            logger, profiler
        );

        SetupBaseComponents(logger, logOptions);

        if (!(args.Contains(CommandOptions.Quiet) || args.Contains(CommandOptions.QuietShort))) {
            foreach (string line in Banner.Lines.Take(Banner.Lines.Length - 1)) {
                AnsiConsole.MarkupLine(line);
            }
            AnsiConsole.MarkupLine(string.Format(Banner.Lines.Last(), ManilaEngine.VERSION) + "\n");
        }

        var manilaEngine = new ManilaEngine(baseServiceContainer, Directories);

        ServiceContainer? serviceContainer = null;

        try {
            var shouldInitialize = true;

            if (!Directory.Exists(Directories.DataDir)) {
                shouldInitialize = false;
                baseServiceContainer.Logger.Debug("Data directory does not exist. Skipping workspace initialization.");
            }

            if (!File.Exists(Path.Join(Directories.RootDir, "Manila.js"))) {
                shouldInitialize = false;
                baseServiceContainer.Logger.Debug("Workspace script file (Manila.js) does not exist. Skipping workspace initialization.");
            }

            if (shouldInitialize) {
                Directory.CreateDirectory(Directories.DataDir);

                var nugetManager = new NuGetManager(logger, profiler, Directories.Nuget);

                serviceContainer = new ServiceContainer(
                    new JobRegistry(),
                    new ArtifactManager(logger, profiler, Directories.Artifacts, Path.Join(Directories.Cache, "artifacts.json")),
                    new ExtensionManager(logger, profiler, Directories.Plugins, nugetManager),
                    nugetManager,
                    new FileHashCache(Path.Join(Directories.DataDir, "cache", "filehashes.db"), Directories.RootDir)
                );

                await serviceContainer.ArtifactManager.LoadCache();

                await InitExtensions(baseServiceContainer, serviceContainer);

                // Run engine and initialize projects
                var workspace = await manilaEngine.RunWorkspaceScript(serviceContainer, new(
                    baseServiceContainer.Logger, baseServiceContainer.Profiler,
                    CreateScriptEngine(),
                    Directories.RootDir, Path.Join(Directories.RootDir, "Manila.js")
                ));

                var workspaceBridge = new WorkspaceScriptBridge(baseServiceContainer.Logger, baseServiceContainer.Profiler, workspace);

                foreach (var script in manilaEngine.DiscoverProjectScripts()) {
                    var projet = await manilaEngine.RunProjectScript(serviceContainer, new(
                        baseServiceContainer.Logger, baseServiceContainer.Profiler,
                        CreateScriptEngine(),
                        Directories.RootDir, script
                    ), workspace, workspaceBridge);
                }

                _ = services.AddSingleton(serviceContainer)
                    .AddSingleton(workspace);
            } else {
                baseServiceContainer.Logger.Debug("No workspace found. Continuing without workspace.");
            }
        } catch (Exception ex) {
            logger.Debug($"Unable initialize Manila engine: {ex.Message}. Continueing without workspace.");
        }

        _ = services.AddSingleton(manilaEngine)
                .AddSingleton(baseServiceContainer)
                .AddSingleton(Directories);
        _ = services
                // Base Commands
                .AddTransient<RunCommand>()
                .AddTransient<PluginsCommand>()
                .AddTransient<JobsCommand>()
                .AddTransient<ArtifactsCommand>()
                .AddTransient<ProjectsCommand>()
                .AddTransient<InitCommand>()

                // API Commands
                .AddTransient<APIArtifactsCommand>()
                .AddTransient<APIJobsCommand>()
                .AddTransient<APIProjectsCommand>()
                .AddTransient<APIPluginsCommand>()
                .AddTransient<APIWorkspaceCommand>();

        CommandApp = new CommandApp<DefaultCommand>(new TypeRegistrar(services));

        CommandApp.Configure(c => {
            c.SetApplicationName("manila");
            c.SetApplicationVersion(ManilaEngine.VERSION);
            c.AddCommand<PluginsCommand>("plugins");
            c.AddCommand<JobsCommand>("jobs");
            c.AddCommand<RunCommand>("run");
            c.AddCommand<ArtifactsCommand>("artifacts");
            c.AddCommand<ProjectsCommand>("projects");
            c.AddCommand<InitCommand>("init");

            c.AddBranch<APICommandSettings>("api", api => {
                api.AddCommand<APIJobsCommand>("jobs");
                api.AddCommand<APIArtifactsCommand>("artifacts");
                api.AddCommand<APIProjectsCommand>("projects");
                api.AddCommand<APIPluginsCommand>("plugins");
                api.AddCommand<APIWorkspaceCommand>("workspace");
            });
        });

        logger.Log(new ProjectsInitializedLogEntry(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - ProgramStartedTime));

        var exitCode = CommandApp.Run(args);
        serviceContainer?.ExtensionManager.ReleasePlugins();

        profiler.SaveToFile(Directories.Profiles);
        serviceContainer?.ArtifactManager.FlushCacheToDisk();

        return exitCode;
    }
}
