using System.Collections.Concurrent;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Shiron.Manila.API.Logging;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Logging;
using Shiron.Manila.Profiling;
using Shiron.Manila.Utils.Logging;
using Spectre.Console;

namespace Shiron.Manila.CLI;

/// <summary>
/// CLI logging configuration flags
/// </summary>
public record LogOptions(
    bool Quiet,
    bool Verbose,
    bool Structured,
    bool StackTrace,
    bool LogProfiling
) {
    /// <summary>
    /// All logging disabled
    /// </summary>
    public static readonly LogOptions None = new(true, false, false, false, false);
    /// <summary>
    /// Default logging behavior
    /// </summary>
    public static readonly LogOptions Default = new(false, false, false, false, false);
}

/// <summary>
/// Renders log entries to console using Spectre
/// </summary>
public static class AnsiConsoleRenderer {
    private static readonly ConcurrentDictionary<LogLevel, string> _logLevelColorMappings = new() {
        [LogLevel.System] = "grey93",
        [LogLevel.Debug] = "dim",
        [LogLevel.Info] = "deepskyblue1",
        [LogLevel.Warning] = "yellow1",
        [LogLevel.Error] = "red1",
        [LogLevel.Critical] = "bold red"
    };

    // --- State Management for Live Display ---
    private static Tree? _executionTree;
    private static Action? _refresh;
    private static TaskCompletionSource<bool>? _buildCompletion;
    private static LiveDisplay? _liveDisplay = null;

    private static LogOptions _options { get; set; } = new(false, false, false, false, false);

    private static readonly ConcurrentDictionary<string, TreeNode> _executionNodes = [];
    private static ILogger? _logger;

    public const int MinProfilingDuration = 10;

    private static readonly object _lock = new();

    /// <summary>
    /// Initializes renderer and attaches log subscription
    /// </summary>
    public static void Init(ILogger logger, LogOptions options) {
        _options = options;

        if (_options.Quiet && _options.Structured) throw new ManilaException("Cannot use quiet logging while structured logging is enabled!");
        _logger = logger;

        var jsonSerializerSettings = new JsonSerializerSettings {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.None,
            Converters = { new StringEnumConverter(), new LogEntryConverter() },
            TypeNameHandling = TypeNameHandling.Auto,
            NullValueHandling = NullValueHandling.Ignore
        };

        _logger.OnLogEntry += entry => {
            if (_options.Structured) {
                Console.WriteLine(JsonConvert.SerializeObject(entry, jsonSerializerSettings));
                return;
            }

            if ((_options.Quiet && entry.Level < LogLevel.Error) || (!_options.Verbose && entry.Level < LogLevel.Info)) {
                return;
            }

            lock (_lock) {
                RenderLog(entry);
            }
        };

        if (_options.Structured && _options.Verbose) { _logger?.Warning("Ignoring 'verbose' flag as the logger is running in structured mode! Logger always logs everything when running on structured mode!"); }
    }

    /// <summary>
    /// Dispatches log entry to specific handler
    /// </summary>
    private static void RenderLog(ILogEntry entry) {
        switch (entry) {
            case BasicLogEntry log:
                HandleBasicLogEntry(log);
                break;
            case MarkupLogEntry log:
                HandleMarkupLogEntry(log);
                break;
            case BasicPluginLogEntry log:
                HandleBasicPluginLogEntry(log);
                break;
            case EngineStartedLogEntry log:
                HandleEngineStartedLogEntry(log);
                break;
            case BuildLayersLogEntry log:
                HandleBuildLayersLogEntry(log);
                break;
            case BuildLayerStartedLogEntry log:
                HandleBuildLayerStartedLogEntry(log);
                break;
            case BuildLayerCompletedLogEntry log:
                HandleBuildLayerCompletedLogEntry(log);
                break;
            case BuildStartedLogEntry log:
                HandleBuildStartedLogEntry(log);
                break;
            case BuildCompletedLogEntry log:
                HandleBuildCompletedLogEntry(log);
                break;
            case BuildFailedLogEntry log:
                HandleBuildFailedLogEntry(log);
                break;
            case ProjectsInitializedLogEntry log:
                HandleProjectsInitializedLogEntry(log);
                break;
            case ScriptExecutionStartedLogEntry log:
                HandleScriptExecutionStartedLogEntry(log);
                break;
            case ScriptLogEntry log:
                HandleScriptLogEntry(log);
                break;
            case ScriptExecutedSuccessfullyLogEntry log:
                HandleScriptExecutedSuccessfullyLogEntry(log);
                break;
            case ScriptExecutionFailedLogEntry log:
                HandleScriptExecutionFailedLogEntry(log);
                break;
            case JobExecutionStartedLogEntry log:
                HandleJobExecutionStartedLogEntry(log);
                break;
            case JobExecutionFinishedLogEntry log:
                HandleJobExecutionFinishedLogEntry(log);
                break;
            case JobExecutionFailedLogEntry log:
                HandleJobExecutionFailedLogEntry(log);
                break;
            case ProjectDiscoveredLogEntry log:
                HandleProjectDiscoveredLogEntry(log);
                break;
            case ProjectInitializedLogEntry log:
                HandleProjectInitializedLogEntry(log);
                break;
            case JobDiscoveredLogEntry log:
                HandleJobDiscoveredLogEntry(log);
                break;
            case CommandExecutionLogEntry log:
                HandleCommandExecutionLogEntry(log);
                break;
            case CommandExecutionFinishedLogEntry log:
                HandleCommandExecutionFinishedLogEntry(log);
                break;
            case CommandExecutionFailedLogEntry log:
                HandleCommandExecutionFailedLogEntry(log);
                break;
            case CommandStdOutLogEntry log:
                HandleCommandStdOutLogEntry(log);
                break;
            case CommandStdErrLogEntry log:
                HandleCommandStdErrLogEntry(log);
                break;
            case StageChangeLogEntry log:
                HandleStageChangeLogEntry(log);
                break;
            case ReplayLogEntry log:
                var logEntry = (BaseLogEntry) log.Entry;
                logEntry.ParentContextID = log.ContextID;
                RenderLog(log.Entry);
                break;

            case NugetManagerDownloadStartEntry log:
                _logger?.Info($"Starting download of NuGet package [yellow]{log.Package}[/] version [yellow]{log.Version}[/]");
                break;
            case NugetManagerDownloadCompleteEntry log:
                _logger?.Info($"Completed download of NuGet package [yellow]{log.Package}[/] version [yellow]{log.Version}[/]");
                break;
            case NuGetPackageLoadingLogEntry log:
                _logger?.Info($"Loading NuGet package [yellow]{log.PackageID}[/] version [yellow]{log.PackageVersion}[/]");
                break;
            case NuGetSubPackageLoadingEntry log:
                _logger?.Info($"Loading NuGet package [yellow]{log.PackageID}[/] version [yellow]{log.PackageVersion}[/]");
                break;

            case ProfileCompleteLogEntry log:
                HandleProfilingCompleteLogEntry(log);
                break;
            case ProfilingLogEntry:
                // Ignore general profiling log entries
                break;

            case LoadingPluginsLogEntry log:
                _logger?.Info($"Loading Manila plugins from [yellow]{log.PluginPath}[/]");
                break;
            case LoadingPluginLogEntry log:
                _logger?.Info($"Loading Manila plugin [yellow]{log.Plugin}[/]");
                break;

            default:
                AnsiConsole.MarkupLine($"[dim]Unhandled Log Event: {entry.GetType().Name}[/]");
                break;
        }
    }

    /// <summary>
    /// Adds joined object array as node or immediate line
    /// </summary>
    private static void PushLog(object[] msg, string? parentID = null, string? contextID = null) {
        PushLog(string.Join(" ", msg), parentID, contextID);
    }
    /// <summary>
    /// Adds message as tree node or prints directly
    /// </summary>
    private static void PushLog(string msg, string? parentID = null, string? contextID = null) {
        if (_executionTree == null || _buildCompletion == null || (_buildCompletion != null && _buildCompletion.Task.IsCompleted)) {
            try {
                AnsiConsole.MarkupLine(msg);
            } catch {
                Console.WriteLine(msg);
            }
            return;
        }

        TreeNode newNode;

        if (parentID != null && _executionNodes.TryGetValue(parentID, out var parentNode)) {
            newNode = parentNode.AddNode(msg);
        } else {
            newNode = _executionTree.AddNode(msg);
        }

        if (contextID != null) {
            _executionNodes[contextID] = newNode;
        }

        _refresh?.Invoke();
    }

    /// <summary>
    /// Handles markup log entries
    /// </summary>
    private static void HandleMarkupLogEntry(MarkupLogEntry entry) {
        PushLog(entry.Message);
    }

    /// <summary>
    /// Handles build start (initializes live display)
    /// </summary>
    private static void HandleBuildStartedLogEntry(BuildStartedLogEntry entry) {
        _buildCompletion = new TaskCompletionSource<bool>();
        _executionTree = new Tree($"[green]{Emoji.Known.Rocket} Build Started![/]");

        _liveDisplay = AnsiConsole.Live(_executionTree)
             .AutoClear(false)
             .Overflow(VerticalOverflow.Ellipsis);

        _ = _liveDisplay.StartAsync(async ctx => {
            _refresh = ctx.Refresh;
            _ = await _buildCompletion.Task;
            ctx.Refresh();
        });
    }

    /// <summary>
    /// Handles layer start
    /// </summary>
    private static void HandleBuildLayerStartedLogEntry(BuildLayerStartedLogEntry entry) {
        PushLog($"[yellow]{Emoji.Known.Package} Layer {entry.LayerIndex}[/]", entry.ParentContextID.ToString(), entry.ContextID);

        _refresh?.Invoke();
    }

    /// <summary>
    /// Handles job execution start
    /// </summary>
    private static void HandleJobExecutionStartedLogEntry(JobExecutionStartedLogEntry entry) {
        // Attach the job to the current layer's node
        PushLog($"[deepskyblue1]Job [skyblue1]{entry.Job.Component.ID}:{entry.Job.Name}[/][/]", entry.ParentContextID.ToString(), entry.ContextID);
        _refresh?.Invoke();
    }

    /// <summary>
    /// Handles build success completion
    /// </summary>
    private static void HandleBuildCompletedLogEntry(BuildCompletedLogEntry entry) {
        // Signal the LiveDisplay to stop
        _buildCompletion?.TrySetResult(true);
        AnsiConsole.MarkupLine($"[green]BUILD SUCCESSFUL![/] [grey]in {entry.Duration}ms[/]");
    }

    /// <summary>
    /// Handles build failure completion
    /// </summary>
    private static void HandleBuildFailedLogEntry(BuildFailedLogEntry entry) {
        // Signal the LiveDisplay to stop
        _buildCompletion?.TrySetResult(true);
        AnsiConsole.MarkupLine($"\n[red]BUILD FAILED![/] [grey]in {entry.Duration}ms[/]");
    }

    /// <summary>
    /// Handles basic log entry
    /// </summary>
    private static void HandleBasicLogEntry(BasicLogEntry entry) {
        PushLog($"[[[{_logLevelColorMappings[entry.Level]}]{entry.Level}[/]]]: {entry.Message}", entry.ParentContextID.ToString());
    }
    /// <summary>
    /// Handles basic plugin log entry
    /// </summary>
    private static void HandleBasicPluginLogEntry(BasicPluginLogEntry entry) {
        PushLog($"[[[grey]{entry.Plugin.Name}[/]/[{_logLevelColorMappings[entry.Level]}]{entry.Level}[/]]]: {entry.Message}", entry.ParentContextID.ToString());
    }
    /// <summary>
    /// Handles engine started
    /// </summary>
    private static void HandleEngineStartedLogEntry(EngineStartedLogEntry entry) {
        _logger?.Info("ManilaEngine started!");
    }
    /// <summary>
    /// Handles layers count log
    /// </summary>
    private static void HandleBuildLayersLogEntry(BuildLayersLogEntry entry) {
        _logger?.Info($"Building using [yellow]{entry.Layers.Length}[/] layers!");
    }
    /// <summary>
    /// Handles build layer completion
    /// </summary>
    private static void HandleBuildLayerCompletedLogEntry(BuildLayerCompletedLogEntry entry) {
        PushLog($"[green]{Emoji.Known.Package} Layer [yellow]{entry.LayerIndex}[/] completed![/]", entry.ParentContextID.ToString(), entry.ContextID);
    }
    /// <summary>
    /// Handles projects initialized log
    /// </summary>
    private static void HandleProjectsInitializedLogEntry(ProjectsInitializedLogEntry entry) {
        _logger?.Info($"Initialization took [yellow]{entry.Duration}[/]ms!");
    }
    /// <summary>
    /// Handles script execution start
    /// </summary>
    private static void HandleScriptExecutionStartedLogEntry(ScriptExecutionStartedLogEntry entry) {
        _logger?.Debug($"Running script {entry.ScriptPath}");
    }
    /// <summary>
    /// Handles script log output
    /// </summary>
    private static void HandleScriptLogEntry(ScriptLogEntry entry) {
        PushLog($"[yellow]>[/] {entry.Message}", entry.ParentContextID.ToString(), entry.ContextID);
    }
    /// <summary>
    /// Handles script success (no-op)
    /// </summary>
    private static void HandleScriptExecutedSuccessfullyLogEntry(ScriptExecutedSuccessfullyLogEntry entry) { }
    /// <summary>
    /// Handles script failure
    /// </summary>
    private static void HandleScriptExecutionFailedLogEntry(ScriptExecutionFailedLogEntry entry) {
        _buildCompletion?.TrySetResult(true);
    }
    /// <summary>
    /// Handles job success completion
    /// </summary>
    private static void HandleJobExecutionFinishedLogEntry(JobExecutionFinishedLogEntry entry) {
        PushLog($"[green]Job [skyblue1]{entry.Job.Name}[/] completed![/]", entry.ParentContextID.ToString(), entry.ContextID);
    }
    /// <summary>
    /// Handles job failure completion
    /// </summary>
    private static void HandleJobExecutionFailedLogEntry(JobExecutionFailedLogEntry entry) {
        _buildCompletion?.TrySetResult(true);
    }
    /// <summary>
    /// Handles project discovery
    /// </summary>
    private static void HandleProjectDiscoveredLogEntry(ProjectDiscoveredLogEntry entry) {
        _logger?.System($"Found project in {entry.Root}");
    }
    /// <summary>
    /// Handles project initialized
    /// </summary>
    private static void HandleProjectInitializedLogEntry(ProjectInitializedLogEntry entry) {
        _logger?.System($"Project {entry.Project.Name} initialized!");
    }
    /// <summary>
    /// Handles job discovery
    /// </summary>
    private static void HandleJobDiscoveredLogEntry(JobDiscoveredLogEntry entry) {
        _logger?.System($"Discovered job {entry.Job.Name} for {entry.Component.Root} in {entry.Component.Root}");
    }
    /// <summary>
    /// Handles command execution start
    /// </summary>
    private static void HandleCommandExecutionLogEntry(CommandExecutionLogEntry entry) {
        PushLog($"[green]$>[/] [grey]{Path.GetFileName(entry.Executable)} {Markup.Escape(string.Join(" ", entry.Args))}[/]", entry.ParentContextID.ToString(), entry.ContextID);
    }
    /// <summary>
    /// Handles command execution success
    /// </summary>
    private static void HandleCommandExecutionFinishedLogEntry(CommandExecutionFinishedLogEntry entry) {
        PushLog($"[green]>[/] [green]Command Exited with exit code {entry.ExitCode}[/]", entry.ParentContextID.ToString(), entry.ContextID);
    }
    /// <summary>
    /// Handles command execution failure
    /// </summary>
    private static void HandleCommandExecutionFailedLogEntry(CommandExecutionFailedLogEntry entry) {
        PushLog($"[green]>[/] [red]Command Exited with exit code {entry.ExitCode}[/]", entry.ParentContextID.ToString(), entry.ContextID);
    }
    /// <summary>
    /// Handles command stdout line
    /// </summary>
    private static void HandleCommandStdOutLogEntry(CommandStdOutLogEntry entry) {
        if (entry.ParentContextID != null) {
            PushLog($"[green]>[/] {entry.Message}", entry.ParentContextID.ToString(), entry.ContextID);
        }
    }

    /// <summary>
    /// Handles execution stage change
    /// </summary>
    private static void HandleStageChangeLogEntry(StageChangeLogEntry entry) {
        var duration = entry.Timestamp - entry.PreviousStartedAt;
        PushLog($"[grey]Stage changed from [blue]{entry.ChangedFrom}[/] to [magenta]{entry.ChangedTo}[/]. Old stage took [yellow]{duration}[/]ms[/]");
    }

    /// <summary>
    /// Handles profiling completion (filters short events)
    /// </summary>
    private static void HandleProfilingCompleteLogEntry(ProfileCompleteLogEntry entry) {
        if (entry.Duration < MinProfilingDuration) return; // Ignore profiling entries that took less than 10ms
        PushLog($"[grey]Profiling event [green]{entry.Name}[/] completed. Duration: [yellow]{entry.Duration}[/]ms[/]");
    }

    /// <summary>
    /// Handles command stderr line (ignored)
    /// </summary>
    private static void HandleCommandStdErrLogEntry(CommandStdErrLogEntry entry) { }
}
