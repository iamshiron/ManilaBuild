using System.Collections.Concurrent;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Shiron.Manila.Caching;
using Shiron.Manila.Logging;
using Spectre.Console;

namespace Shiron.Manila.CLI;

public record LogOptions(
    bool Quiet,
    bool Verbose,
    bool Structured,
    bool StackTrace
);

/// <summary>
/// Handles rendering structured log entries into a human-readable format
/// using Spectre.Console, creating a rich, structured view of the build process.
/// This version leverages more Spectre.Console features like Rules, Panels, and Charts
/// for a cleaner and more informative output.
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
    private static Action? _refresh; // Action to refresh the LiveDisplay
    private static TaskCompletionSource<bool>? _buildCompletion; // Controls the LiveDisplay lifetime
    private static readonly Dictionary<string, TaskCompletionSource> _nodeCompletiosn = [];

    private static LogOptions _options { get; set; } = new(false, false, false, false);

    private static readonly ConcurrentDictionary<string, TreeNode> _executionNodes = [];
    private static ILogger? _logger;

    private static readonly object _lock = new();

    /// <summary>
    /// Initializes the console renderer, subscribing to the logger's OnLogEntry event.
    /// </summary>
    /// <param name="quiet">If true, only logs with level Error or higher.</param>
    /// <param name="structured">If true, outputs raw JSON instead of rendering.</param>
    public static void Init(ILogger logger, LogOptions options) {
        _options = options;

        if (_options.Quiet && _options.Structured) throw new Exception("Cannot use quiet logging while structured logging is enabled!");
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

            // Filter logs based on the quiet flag and log level.
            if ((_options.Quiet && entry.Level < LogLevel.Error) || (!_options.Verbose && entry.Level < LogLevel.Info)) {
                return;
            }

            lock (_lock) {
                RenderLog(entry);
            }
        };

        if (_options.Structured && _options.Verbose) { _logger?.Warning("Ignoring 'verbose' flag is the logger is running in structured mode!"); _logger?.Info("Logger always logs everything when running on structured mode!"); }
    }

    /// <summary>
    /// Renders a single log entry by dispatching to the appropriate handler.
    /// </summary>
    /// <param name="entry">The log entry to render.</param>
    private static void RenderLog(ILogEntry entry) {
        // Dispatch to the correct handler based on the concrete log entry type.
        switch (entry) {
            case BasicLogEntry log:
                HandleBasicLogEntry(log);
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
            case ReplayLogEntry log:
                var logEntry = (BaseLogEntry) log.Entry;
                logEntry.ParentContextID = log.ContextID;
                RenderLog(log.Entry);
                break;
            // Default fallback for any unhandled log types
            default:
                AnsiConsole.MarkupLine($"[dim]Unhandled Log Event: {entry.GetType().Name}[/]");
                break;
        }
    }

    private static void PushLog(object[] msg, string? parentID = null, string? contextID = null) {
        PushLog(string.Join(" ", msg), parentID, contextID);
    }
    private static void PushLog(string msg, string? parentID = null, string? contextID = null) {
        // Fallback for when the live display isn't active.
        if (_executionTree is null) {
            AnsiConsole.MarkupLine(msg);
            return;
        }

        TreeNode newNode;

        // Check if a parentID is provided and if that parent exists in our node map.
        if (parentID != null && _executionNodes.TryGetValue(parentID, out var parentNode)) {
            // A valid parent was found, so attach this log as a child node.
            newNode = parentNode.AddNode(msg);
        } else {
            // If no parent is specified or the parentID is invalid, attach the log
            // to the root of the tree to prevent it from being lost.
            newNode = _executionTree.AddNode(msg);
        }

        // If this log entry has its own contextID, register its new node so that
        // subsequent logs can attach to it.
        if (contextID != null) {
            _executionNodes[contextID] = newNode;
        }

        // Trigger a refresh of the live display to show the new node.
        _refresh?.Invoke();
    }

    private static void HandleBuildStartedLogEntry(BuildStartedLogEntry entry) {
        _buildCompletion = new TaskCompletionSource<bool>();
        _executionTree = new Tree($"[green]{Emoji.Known.Rocket} Build Started![/]");

        AnsiConsole.Live(_executionTree)
            .AutoClear(false)
            .Overflow(VerticalOverflow.Ellipsis)
            .StartAsync(async ctx => {
                _refresh = ctx.Refresh;
                _ = await _buildCompletion.Task;
                ctx.Refresh();
            });
    }

    private static void HandleBuildLayerStartedLogEntry(BuildLayerStartedLogEntry entry) {
        PushLog($"[yellow]{Emoji.Known.Package} Layer {entry.LayerIndex}[/]", entry.ParentContextID.ToString(), entry.ContextID);

        _refresh?.Invoke();
    }

    private static void HandleJobExecutionStartedLogEntry(JobExecutionStartedLogEntry entry) {
        // Attach the job to the current layer's node
        PushLog($"[deepskyblue1]Job [skyblue1]{entry.Job.Name}[/][/]", entry.ParentContextID.ToString(), entry.ContextID);
        _refresh?.Invoke();
    }

    private static void HandleBuildCompletedLogEntry(BuildCompletedLogEntry entry) {
        // Signal the LiveDisplay to stop
        _buildCompletion?.TrySetResult(true);
        AnsiConsole.MarkupLine($"[green]BUILD SUCCESSFUL![/] [grey]in {entry.Duration}ms[/]");
    }

    private static void HandleBuildFailedLogEntry(BuildFailedLogEntry entry) {
        // Signal the LiveDisplay to stop
        _buildCompletion?.TrySetResult(true);
        AnsiConsole.MarkupLine($"\n[red]BUILD FAILED![/] [grey]in {entry.Duration}ms[/]");
    }

    private static void HandleBasicLogEntry(BasicLogEntry entry) {
        PushLog($"[[[{_logLevelColorMappings[entry.Level]}]{entry.Level}[/]]]: {entry.Message}", entry.ParentContextID.ToString());
    }
    private static void HandleBasicPluginLogEntry(BasicPluginLogEntry entry) {
        PushLog($"[[[grey]{entry.Plugin.Name}[/]/[{_logLevelColorMappings[entry.Level]}]{entry.Level}[/]]]: {entry.Message}", entry.ParentContextID.ToString());
    }
    private static void HandleEngineStartedLogEntry(EngineStartedLogEntry entry) {
        _logger?.Info("ManilaEngine started!");
    }
    private static void HandleBuildLayersLogEntry(BuildLayersLogEntry entry) {
        _logger?.Info($"Building using [yellow]{entry.Layers.Length}[/] layers!");
    }
    private static void HandleBuildLayerCompletedLogEntry(BuildLayerCompletedLogEntry entry) {
        PushLog($"[green]{Emoji.Known.Package} Layer [yellow]{entry.LayerIndex}[/] completed![/]", entry.ParentContextID.ToString(), entry.ContextID);
    }
    private static void HandleProjectsInitializedLogEntry(ProjectsInitializedLogEntry entry) {
        _logger?.Info($"Initialization took [yellow]{entry.Duration}[/]ms!");
    }
    private static void HandleScriptExecutionStartedLogEntry(ScriptExecutionStartedLogEntry entry) {
        _logger?.Debug($"Running script {entry.ScriptPath}");
    }
    private static void HandleScriptLogEntry(ScriptLogEntry entry) {
        PushLog($"[yellow]>[/] {entry.Message}", entry.ParentContextID.ToString(), entry.ContextID);
    }
    private static void HandleScriptExecutedSuccessfullyLogEntry(ScriptExecutedSuccessfullyLogEntry entry) { }
    private static void HandleScriptExecutionFailedLogEntry(ScriptExecutionFailedLogEntry entry) {
        _buildCompletion?.TrySetResult(true);
    }
    private static void HandleJobExecutionFinishedLogEntry(JobExecutionFinishedLogEntry entry) {
        PushLog($"[green]Job [skyblue1]{entry.Job.Name}[/] completed![/]", entry.ParentContextID.ToString(), entry.ContextID);
    }
    private static void HandleJobExecutionFailedLogEntry(JobExecutionFailedLogEntry entry) {
        _buildCompletion?.TrySetResult(true);
    }
    private static void HandleProjectDiscoveredLogEntry(ProjectDiscoveredLogEntry entry) {
        _logger?.System($"Found project in {entry.Root}");
    }
    private static void HandleProjectInitializedLogEntry(ProjectInitializedLogEntry entry) {
        _logger?.System($"Project {entry.Project.Name} initialized!");
    }
    private static void HandleJobDiscoveredLogEntry(JobDiscoveredLogEntry entry) {
        _logger?.System($"Discovered job {entry.Job.Name} for {entry.Component.Root} in {entry.Component.Root}");
    }
    private static void HandleCommandExecutionLogEntry(CommandExecutionLogEntry entry) {
        PushLog($"[green]$>[/] [grey]{Path.GetFileName(entry.Executable)} {Markup.Escape(string.Join(" ", entry.Args))}[/]", entry.ParentContextID.ToString(), entry.ContextID);
    }
    private static void HandleCommandExecutionFinishedLogEntry(CommandExecutionFinishedLogEntry entry) {
        PushLog($"[green]>[/] [green]Command Exited with exit code {entry.ExitCode}[/]", entry.ParentContextID.ToString(), entry.ContextID);
    }
    private static void HandleCommandExecutionFailedLogEntry(CommandExecutionFailedLogEntry entry) {
        PushLog($"[green]>[/] [red]Command Exited with exit code {entry.ExitCode}[/]", entry.ParentContextID.ToString(), entry.ContextID);
    }
    private static void HandleCommandStdOutLogEntry(CommandStdOutLogEntry entry) {
        if (entry.ParentContextID != null) {
            PushLog($"[green]>[/] {entry.Message}", entry.ParentContextID.ToString(), entry.ContextID);
        }
    }
    private static void HandleCommandStdErrLogEntry(CommandStdErrLogEntry entry) { }
}
