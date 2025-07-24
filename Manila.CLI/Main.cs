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

public static class ManilaCli {
    public static CommandApp<DefaultCommand>? CommandApp { get; private set; }
    public static IDirectories? Directories { get; private set; }
    public static readonly long ProgramStartedTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public static void SetupBaseComponents(ILogger logger, LogOptions logOptions) {
        AnsiConsoleRenderer.Init(logger, logOptions);
    }

    private static async Task InitExtensions(BaseServiceCotnainer baseServices, ServiceContainer services) {
        using (new ProfileScope(baseServices.Profiler, "Initializing Extensons")) {
            if (Directories == null) {
                throw new UnableToInitializeEngineException("Directories are not initialized.");
            }

            using (new ProfileScope(baseServices.Profiler, "Initializing Plugins")) {
                await services.ExtensionManager.LoadPluginsAsync();
                services.ExtensionManager.InitPlugins();
            }
        }
    }

    private static V8ScriptEngine CreateScriptEngine() {
        var engine = new V8ScriptEngine(
            V8ScriptEngineFlags.EnableTaskPromiseConversion
        ) {
            ExposeHostObjectStaticMembers = true,
            CustomAttributeLoader = new JavaScriptAttributeLoader()
        };

        return engine;
    }

    public static async Task<int> RunJobAsync(ServiceContainer services, BaseServiceCotnainer baseServices, ManilaEngine engine, Workspace workspace, DefaultCommandSettings settings, string job) {
        var graph = engine.CreateExecutionGraph(services, baseServices, workspace);

        var task = ErrorHandler.SafeExecuteAsync(async () => {
            await engine.ExecuteBuildLogicAsync(graph, job);
            return ExitCodes.SUCCESS;
        }, settings.ToLogOptions());

        return task != null ? await task : ExitCodes.USER_COMMAND_ERROR;
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

        var commands = CommandUtils.GetCommandNames(args);
        var isApiCommand = commands.Length > 0 && commands[0] == "api";
        var ignoreBanner = logOptions.Quiet || isApiCommand;

        Directories = new Directories();

        var logger = isApiCommand ? (ILogger) new EmptyLogger() : new Logger(null);
        var profiler = new Profiler(logger);
        var services = new ServiceCollection();

        var baseServiceContainer = new BaseServiceCotnainer(
            logger, profiler
        );

        SetupBaseComponents(logger, logOptions);

        if (!ignoreBanner) {
            foreach (string line in Banner.Lines.Take(Banner.Lines.Length - 1)) {
                AnsiConsole.MarkupLine(line);
            }
            AnsiConsole.MarkupLine(string.Format(Banner.Lines[Banner.Lines.Length - 1], ManilaEngine.VERSION) + "\n");
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
                using (new ProfileScope(baseServiceContainer.Profiler, "Initializing Manila Engine")) {
                    Directory.CreateDirectory(Directories.DataDir);

                    var nugetManager = new NuGetManager(logger, profiler, Directories.Nuget);

                    serviceContainer = new ServiceContainer(
                        new JobRegistry(baseServiceContainer.Profiler),
                        new ArtifactManager(logger, profiler, Directories.Artifacts, Path.Join(Directories.Cache, "artifacts.json")),
                        new ExtensionManager(logger, profiler, Directories.Plugins, nugetManager),
                        nugetManager,
                        new FileHashCache(baseServiceContainer.Profiler, Path.Join(Directories.DataDir, "cache", "filehashes.db"), Directories.RootDir)
                    );

                    serviceContainer.ArtifactManager.LoadCache();
                    await InitExtensions(baseServiceContainer, serviceContainer);

                    // Run engine and initialize projects
                    var workspace = await manilaEngine.RunWorkspaceScriptAsync(serviceContainer, new(
                        baseServiceContainer.Logger, baseServiceContainer.Profiler,
                        CreateScriptEngine(),
                        Directories.RootDir, Path.Join(Directories.RootDir, "Manila.js")
                    ));

                    var workspaceBridge = new WorkspaceScriptBridge(baseServiceContainer.Logger, baseServiceContainer.Profiler, workspace);

                    List<Task<Project>> projectInitializationTasks = [];
                    foreach (var script in manilaEngine.DiscoverProjectScripts(baseServiceContainer.Profiler)) {
                        projectInitializationTasks.Add(
                            manilaEngine.RunProjectScriptAsync(serviceContainer, new(
                                baseServiceContainer.Logger, baseServiceContainer.Profiler,
                                CreateScriptEngine(),
                                Directories.RootDir, script
                            ), workspace, workspaceBridge)
                        );
                    }
                    baseServiceContainer.Logger.Debug($"Discovered {projectInitializationTasks.Count} project scripts.");
                    await Task.WhenAll(projectInitializationTasks);
                    baseServiceContainer.Logger.Debug("All projects initialized successfully.");

                    _ = services.AddSingleton(serviceContainer)
                        .AddSingleton(workspace);
                }
            } else {
                baseServiceContainer.Logger.Debug("No workspace found. Continuing without workspace.");
            }
        } catch (UnableToInitializeEngineException e) {
            logger.Debug($"Unable initialize Manila engine: {e.Message}. Continueing without workspace.");
        } catch (Exception e) {
            return ErrorHandler.HandleException(e, logOptions);
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
            c.Settings.ShowOptionDefaultValues = true;

            c.SetApplicationName("manila");
            c.SetApplicationVersion(ManilaEngine.VERSION);

            c.AddCommand<InitCommand>("init");
            c.AddCommand<RunCommand>("run");
            c.AddCommand<JobsCommand>("jobs");
            c.AddCommand<ArtifactsCommand>("artifacts");
            c.AddCommand<ProjectsCommand>("projects");
            c.AddCommand<PluginsCommand>("plugins");

            c.AddBranch<APICommandSettings>("api", api => {
                api.SetDescription("API commands for retrieving information as json.");
                api.AddCommand<APIJobsCommand>("jobs");
                api.AddCommand<APIArtifactsCommand>("artifacts");
                api.AddCommand<APIProjectsCommand>("projects");
                api.AddCommand<APIPluginsCommand>("plugins");
                api.AddCommand<APIWorkspaceCommand>("workspace");
                api.AddCommand<APIBuildLayersCommand>("layers");
                api.AddCommand<APIGraphCommand>("graph");
            });
        });

        logger.Log(new ProjectsInitializedLogEntry(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - ProgramStartedTime));

        var exitCode = await CommandApp.RunAsync(args);
        serviceContainer?.ExtensionManager.ReleasePlugins();

        profiler.SaveToFile(Directories.Profiles);
        serviceContainer?.ArtifactManager.FlushCacheToDisk();

        return exitCode;
    }
}
