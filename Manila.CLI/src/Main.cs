using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NuGet.Packaging.Signing;
using Shiron.Manila;
using Shiron.Manila.API;
using Shiron.Manila.API.Bridges;
using Shiron.Manila.API.Builders;
using Shiron.Manila.API.Exceptions;
using Shiron.Manila.API.Interfaces;
using Shiron.Manila.Caching;
using Shiron.Manila.CLI.Commands;
using Shiron.Manila.CLI.Commands.API;
using Shiron.Manila.CLI.Utils;
using Shiron.Manila.Logging;
using Shiron.Manila.Profiling;
using Shiron.Manila.Registries;
using Shiron.Manila.Services;
using Shiron.Manila.Utils;
using Spectre.Console;
using Spectre.Console.Cli;
using static Shiron.Manila.CLI.CLIConstants;

namespace Shiron.Manila.CLI;

public static class ManilaCli {
    public static CommandApp<DefaultCommand>? CommandApp { get; private set; }
    public static IDirectories? Directories { get; private set; }
    public static readonly long ProgramStartedTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public static ExecutionStage? ExecutionStage { get; private set; }

    public static void SetupBaseComponents(ILogger logger, LogOptions logOptions) {
        AnsiConsoleRenderer.Init(logger, logOptions);
    }

    private static async Task InitExtensions(BaseServiceContainer baseServices, ServiceContainer services) {
        using (new ProfileScope(baseServices.Profiler, "Initializing Extensions")) {
            if (Directories == null) {
                throw new ManilaException("Directories are not initialized.");
            }

            using (new ProfileScope(baseServices.Profiler, "Initializing Plugins")) {
                await services.ExtensionManager.LoadPluginsAsync();
                services.ExtensionManager.InitPlugins();
            }
        }
    }

    public static async Task<int> RunJobAsync(ServiceContainer services, BaseServiceContainer baseServices, ManilaEngine engine, Workspace workspace, DefaultCommandSettings settings, string job) {
        var graph = engine.CreateExecutionGraph(services, baseServices, workspace);

        var task = ErrorHandler.SafeExecuteAsync(baseServices.Logger, async () => {
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
        ExecutionStage = new ExecutionStage(logger);

        var baseServiceContainer = new BaseServiceContainer(
            logger, profiler, ExecutionStage
        );

        SetupBaseComponents(logger, logOptions);

        if (!ignoreBanner) {
            foreach (string line in Banner.Lines.Take(Banner.Lines.Length - 1)) {
                AnsiConsole.MarkupLine(line);
            }
            AnsiConsole.MarkupLine(string.Format(Banner.Lines[^1], ManilaEngine.VERSION) + "\n");
        }

        var manilaEngine = new ManilaEngine(baseServiceContainer, Directories);

        ServiceContainer? serviceContainer = null;

        try {
            var shouldInitialize = true;
            var dataDirExists = Directory.Exists(Directories.Data);
            var workspaceFileExists = File.Exists(Path.Join(Directories.Root, "Manila.js"));

            if (!dataDirExists) {
                shouldInitialize = false;
                baseServiceContainer.Logger.Debug("Data directory does not exist. Skipping workspace initialization.");
            }
            if (!workspaceFileExists) {
                shouldInitialize = false;
                baseServiceContainer.Logger.Debug("Workspace script file (Manila.js) does not exist. Skipping workspace initialization.");
            }

            if (shouldInitialize) {
                using (new ProfileScope(baseServiceContainer.Profiler, "Initializing Manila Engine")) {
                    ExecutionStage.ChangeState(ExecutionStages.Discovery);

                    Directory.CreateDirectory(Directories.Data);

                    var nugetManager = new NuGetManager(logger, profiler, Directories.Nuget);

                    IArtifactCache artifactCache = Environment.GetEnvironmentVariable("MANILA_CACHE_HOST") == null ?
                        new ArtifactCache(logger, Directories.Artifacts, Path.Join(Directories.Cache, "artifacts.json")) :
                        new RemoteArtifactCache(
                            Environment.GetEnvironmentVariable("MANILA_CACHE_HOST")!,
                            Environment.GetEnvironmentVariable("MANILA_CACHE_KEY") ?? "",
                            logger,
                            Directories,
                            new ArtifactCache(logger, Directories.Artifacts, Path.Join(Directories.Cache, "artifacts.json"))
                        );

                    if (!await artifactCache.CheckAvailability()) {
                        throw new ManilaException($"Artifact cache of type '{artifactCache.GetType().FullName}' is not available. Aborting initialization.");
                    }

                    logger.Debug($"Using artifact cache of type '{artifactCache.GetType().FullName}'.");

                    serviceContainer = new ServiceContainer(
                        new JobRegistry(baseServiceContainer.Profiler),
                        new ArtifactManager(logger, profiler, Directories.Artifacts, artifactCache),
                        artifactCache,
                        new ExtensionManager(logger, profiler, Directories.Plugins, nugetManager),
                        nugetManager,
                        new FileHashCache(baseServiceContainer.Profiler, Directories)
                    );

                    serviceContainer.ArtifactCache.LoadCache();
                    await InitExtensions(baseServiceContainer, serviceContainer);

                    // Run engine and initialize projects
                    ExecutionStage.ChangeState(ExecutionStages.Configuration);
                    var workspace = await manilaEngine.RunWorkspaceScriptAsync(serviceContainer, new(
                        baseServiceContainer.Logger, baseServiceContainer.Profiler, serviceContainer.ExtensionManager,
                        Directories.Root, Path.Join(Directories.Root, "Manila.js")
                    ));

                    var workspaceBridge = new WorkspaceScriptBridge(baseServiceContainer.Logger, baseServiceContainer.Profiler, workspace);

                    List<Task<Project>> projectInitializationTasks = [];
                    foreach (var script in manilaEngine.DiscoverProjectScripts(baseServiceContainer.Profiler)) {
                        projectInitializationTasks.Add(
                            manilaEngine.RunProjectScriptAsync(serviceContainer, new(
                                baseServiceContainer.Logger, baseServiceContainer.Profiler, serviceContainer.ExtensionManager,
                                Directories.Root, script
                            ), workspace, workspaceBridge)
                        );
                    }
                    baseServiceContainer.Logger.Debug($"Discovered {projectInitializationTasks.Count} project scripts.");
                    await Task.WhenAll(projectInitializationTasks);
                    baseServiceContainer.Logger.Debug("All projects initialized successfully.");

                    foreach (var project in workspace.Projects.Values) {
                        foreach (var artifact in project.Artifacts.Values) {
                            foreach (var dep in artifact.Dependencies) {
                                dep.Resolve(artifact);
                            }
                        }
                    }

                    _ = services.AddSingleton(serviceContainer)
                        .AddSingleton(workspace);
                }
            } else {
                baseServiceContainer.Logger.Debug("No workspace found. Continuing without workspace.");
            }
        } catch (Exception e) {
            return ErrorHandler.Handle(baseServiceContainer.Logger, e, logOptions);
        }

        _ = services.AddSingleton(manilaEngine)
                .AddSingleton(baseServiceContainer)
                .AddSingleton(Directories)
                .AddSingleton(baseServiceContainer.Logger);
        _ = services
                // Base Commands
                .AddTransient<RunCommand>()
                .AddTransient<PluginsCommand>()
                .AddTransient<TemplatesCommand>()
                .AddTransient<JobsCommand>()
                .AddTransient<NewCommand>()
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
            c.AddCommand<NewCommand>("new");
            c.AddCommand<RunCommand>("run");
            c.AddCommand<JobsCommand>("jobs");
            c.AddCommand<ArtifactsCommand>("artifacts");
            c.AddCommand<ProjectsCommand>("projects");
            c.AddCommand<TemplatesCommand>("templates");
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

        ExecutionStage.ChangeState(ExecutionStages.Runtime);
        var exitCode = await CommandApp.RunAsync(args);
        ExecutionStage.ChangeState(ExecutionStages.Shutdown);

        serviceContainer?.ExtensionManager.ReleasePlugins();

        profiler.SaveToFile(Directories.Profiles);
        serviceContainer?.ArtifactCache.FlushCacheToDisk();
        return exitCode;
    }
}
