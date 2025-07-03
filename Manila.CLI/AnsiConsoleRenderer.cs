using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using NuGet.Protocol;
using Shiron.Manila.Logging;
using Spectre.Console;
using Spectre.Console.Rendering;
using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics;
using System.Text;

/// <summary>
/// Handles rendering structured log entries into a human-readable format
/// using Spectre.Console, creating a rich, structured view of the build process.
/// This version leverages more Spectre.Console features like Rules, Panels, and Charts
/// for a cleaner and more informative output.
/// </summary>
public static class AnsiConsoleRenderer {
    private static readonly ConcurrentDictionary<LogLevel, string> LogLevelColorMappings = new() {
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
    private static Dictionary<string, TaskCompletionSource> _nodeCompletiosn = [];

    private static readonly ConcurrentDictionary<string, TreeNode> _executionNodes = [];

    private static readonly object _lock = new();

    /// <summary>
    /// Initializes the console renderer, subscribing to the logger's OnLogEntry event.
    /// </summary>
    /// <param name="quiet">If true, only logs with level Error or higher.</param>
    /// <param name="structured">If true, outputs raw JSON instead of rendering.</param>
    public static void Init(bool quiet, bool structured) {
        var jsonSerializerSettings = new JsonSerializerSettings {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.None,
            Converters = { new StringEnumConverter(), new LogEntryConverter() },
            TypeNameHandling = TypeNameHandling.Auto,
            NullValueHandling = NullValueHandling.Ignore
        };

        Logger.OnLogEntry += entry => {
            if (structured) {
                AnsiConsole.WriteLine(JsonConvert.SerializeObject(entry, jsonSerializerSettings));
                return;
            }

            // Filter logs based on the quiet flag and log level.
            if ((quiet && entry.Level < LogLevel.Error) || entry.Level < LogLevel.Debug) {
                return;
            }

            lock (_lock) {
                RenderLog(entry);
            }
        };
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
            case TaskExecutionStartedLogEntry log:
                HandleTaskExecutionStartedLogEntry(log);
                break;
            case TaskExecutionFinishedLogEntry log:
                HandleTaskExecutionFinishedLogEntry(log);
                break;
            case TaskExecutionFailedLogEntry log:
                HandleTaskExecutionFailedLogEntry(log);
                break;
            case ProjectDiscoveredLogEntry log:
                HandleProjectDiscoveredLogEntry(log);
                break;
            case ProjectInitializedLogEntry log:
                HandleProjectInitializedLogEntry(log);
                break;
            case TaskDiscoveredLogEntry log:
                HandleTaskDiscoveredLogEntry(log);
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
        _executionTree = new Tree($"[green]🚀 Build Started![/]");

        AnsiConsole.Live(_executionTree)
            .AutoClear(false)
            .Overflow(VerticalOverflow.Ellipsis)
            .StartAsync(async ctx => {
                _refresh = ctx.Refresh; // Store the refresh delegate
                await _buildCompletion.Task; // Wait until build is marked as complete
                _executionTree.AddNode("[bold blue]✔️ Build Finished![/]");
                ctx.Refresh();
            });
    }

    private static void HandleBuildLayerStartedLogEntry(BuildLayerStartedLogEntry entry) {
        PushLog("[yellow]📦 Layer[/]", entry.ParentContextID.ToString(), entry.ContextID);

        _refresh?.Invoke();
    }

    private static void HandleTaskExecutionStartedLogEntry(TaskExecutionStartedLogEntry entry) {
        // Attach the task to the current layer's node
        PushLog($"[deepskyblue1]Task [skyblue1]{entry.Task.Name}[/][/]", entry.ParentContextID.ToString(), entry.ContextID);
        _refresh?.Invoke();
    }

    private static void HandleBuildCompletedLogEntry(BuildCompletedLogEntry entry) {
        // Signal the LiveDisplay to stop
        _buildCompletion?.SetResult(true);
    }

    private static void HandleBuildFailedLogEntry(BuildFailedLogEntry entry) {
        if (_executionTree is not null) {
            _executionTree.AddNode($"[bold red]❌ Build Failed: {entry.Exception.Message}[/]");
            _refresh?.Invoke();
        }
        // Signal the LiveDisplay to stop
        _buildCompletion?.SetResult(true);
    }

    private static void HandleBasicLogEntry(BasicLogEntry entry) { }
    private static void HandleBasicPluginLogEntry(BasicPluginLogEntry entry) { }
    private static void HandleEngineStartedLogEntry(EngineStartedLogEntry entry) { }
    private static void HandleBuildLayersLogEntry(BuildLayersLogEntry entry) { }
    private static void HandleBuildLayerCompletedLogEntry(BuildLayerCompletedLogEntry entry) {
    }
    private static void HandleProjectsInitializedLogEntry(ProjectsInitializedLogEntry entry) { }
    private static void HandleScriptExecutionStartedLogEntry(ScriptExecutionStartedLogEntry entry) { }
    private static void HandleScriptLogEntry(ScriptLogEntry entry) {
        PushLog($"[yellow]>[/] {entry.Message}", entry.ParentContextID.ToString(), entry.ContextID);
    }
    private static void HandleScriptExecutedSuccessfullyLogEntry(ScriptExecutedSuccessfullyLogEntry entry) { }
    private static void HandleScriptExecutionFailedLogEntry(ScriptExecutionFailedLogEntry entry) { }
    private static void HandleTaskExecutionFinishedLogEntry(TaskExecutionFinishedLogEntry entry) { }
    private static void HandleTaskExecutionFailedLogEntry(TaskExecutionFailedLogEntry entry) { }
    private static void HandleProjectDiscoveredLogEntry(ProjectDiscoveredLogEntry entry) { }
    private static void HandleProjectInitializedLogEntry(ProjectInitializedLogEntry entry) { }
    private static void HandleTaskDiscoveredLogEntry(TaskDiscoveredLogEntry entry) { }
    private static void HandleCommandExecutionLogEntry(CommandExecutionLogEntry entry) {
        PushLog($"[grey]{Path.GetFileName(entry.Executable)}[/]", entry.ParentContextID.ToString(), entry.ContextID);
    }
    private static void HandleCommandExecutionFinishedLogEntry(CommandExecutionFinishedLogEntry entry) { }
    private static void HandleCommandExecutionFailedLogEntry(CommandExecutionFailedLogEntry entry) { }
    private static void HandleCommandStdOutLogEntry(CommandStdOutLogEntry entry) {
        if (entry.ParentContextID != null) {
            PushLog(entry.Message, entry.ParentContextID.ToString(), entry.ContextID);
        }
    }
    private static void HandleCommandStdErrLogEntry(CommandStdErrLogEntry entry) { }
}
