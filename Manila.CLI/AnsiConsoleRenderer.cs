using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Shiron.Manila.Logging;
using Spectre.Console;
using System.Diagnostics;
using System.Text;

/// <summary>
/// Handles rendering structured log entries into a human-readable format
/// using Spectre.Console, creating a tree-like view of the build process.
/// </summary>
public static class AnsiConsoleRenderer {
    private static readonly Stack<TaskInfo> _taskStack = new();
    private static readonly Dictionary<string, long> _taskTimers = new();

    private static readonly Dictionary<Type, Action<ILogEntry>> _renderActions = new()
    {
        // Build Lifecycle Events
        { typeof(ProjectsInitializedLogEntry), e => HandleProjectsInitialized((ProjectsInitializedLogEntry)e) },
        { typeof(BuildStartedLogEntry), e => HandleBuildStarted((BuildStartedLogEntry)e) },
        { typeof(BuildLayerStartedLogEntry), e => HandleBuildLayerStarted((BuildLayerStartedLogEntry)e) },
        { typeof(BuildCompletedLogEntry), e => HandleBuildCompleted((BuildCompletedLogEntry)e) },
        { typeof(BuildFailedLogEntry), e => HandleBuildFailed((BuildFailedLogEntry)e) },

        // Task Execution Events
        { typeof(TaskExecutionStartedLogEntry), e => HandleTaskExecutionStarted((TaskExecutionStartedLogEntry)e) },
        { typeof(TaskExecutionFinishedLogEntry), e => HandleTaskExecutionFinished((TaskExecutionFinishedLogEntry)e) },
        { typeof(TaskExecutionFailedLogEntry), e => HandleTaskExecutionFailed((TaskExecutionFailedLogEntry)e) },

        // Plugin & Script Messages
        { typeof(BasicPluginLogEntry), e => HandleBasicPluginLog((BasicPluginLogEntry)e) },
        { typeof(BasicLogEntry), e => HandleBasicLog((BasicLogEntry)e) },
        { typeof(ScriptLogEntry), e => HandleScriptLog((ScriptLogEntry)e) },
        { typeof(ScriptExecutionFailedLogEntry), e => HandleScriptExecutionFailed((ScriptExecutionFailedLogEntry)e) },

        // Command Output Events
        { typeof(CommandStdOutLogEntry), e => HandleCommandStdOut((CommandStdOutLogEntry)e) },
        { typeof(CommandStdErrLogEntry), e => HandleCommandStdErr((CommandStdErrLogEntry)e) }
    };

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
                Console.WriteLine(JsonConvert.SerializeObject(entry, jsonSerializerSettings));
                return;
            }

            if ((quiet && entry.Level < LogLevel.Error) || (!quiet && entry.Level < LogLevel.Info)) {
                return;
            }

            RenderLog(entry);
        };
    }

    /// <summary>
    /// Renders a single log entry by dispatching to the appropriate handler.
    /// </summary>
    /// <param name="entry">The log entry to render.</param>
    private static void RenderLog(ILogEntry entry) {
        if (_renderActions.TryGetValue(entry.GetType(), out var renderAction)) {
            renderAction(entry);
        }
        // Logs without a specific handler are silently ignored.
    }

    // --- Handler Implementations ---

    private static void HandleProjectsInitialized(ProjectsInitializedLogEntry entry) {
        AnsiConsole.MarkupLine($"[white]Project initialization took:[/] [green]{entry.Duration}ms[/]\n");
    }

    private static void HandleBuildStarted(BuildStartedLogEntry entry) {
        AnsiConsole.MarkupLine("[bold cornflowerblue]▶▶▶ BUILD STARTED[/]");
    }

    private static void HandleBuildLayerStarted(BuildLayerStartedLogEntry entry) {
        var items = string.Join(", ", entry.Layer.Items.Select(i => $"[bold]{i.ID}[/]"));
        RenderTreeMessage($"[bold]Executing Layer:[/] {items}", " слой");
    }

    private static void HandleBuildCompleted(BuildCompletedLogEntry entry) {
        AnsiConsole.MarkupLine($"\n[bold green]✔ BUILD SUCCESSFUL[/] [dim]in {entry.Duration}ms[/]");
    }

    private static void HandleBuildFailed(BuildFailedLogEntry entry) {
        AnsiConsole.MarkupLine($"\n[bold red]❌ BUILD FAILED[/] [dim]after {entry.Duration}ms[/]");
        RenderExceptionPanel(entry.Exception, "Build Failure");
    }

    private static void HandleTaskExecutionStarted(TaskExecutionStartedLogEntry entry) {
        _taskStack.Push(entry.Task);
        _taskTimers[entry.Task.ID] = Stopwatch.GetTimestamp();
        RenderTreeMessage($"[cyan]▶[/] [bold]{entry.Task.Name}[/]", "├─");
    }

    private static void HandleTaskExecutionFinished(TaskExecutionFinishedLogEntry entry) {
        // Pop the corresponding task from the stack to correctly finish the tree branch.
        if (_taskStack.TryPeek(out var currentTask) && currentTask.ID == entry.Task.ID) {
            _taskStack.Pop();
        }

        if (_taskTimers.TryGetValue(entry.Task.ID, out var startTime)) {
            var duration = Stopwatch.GetElapsedTime(startTime);
            RenderTreeMessage($"[green]✓[/] [bold]{entry.Task.Name}[/] [green]completed[/] [dim]in {duration.TotalMilliseconds:F0}ms[/]", "└─");
            _taskTimers.Remove(entry.Task.ID);
        }
    }

    private static void HandleTaskExecutionFailed(TaskExecutionFailedLogEntry entry) {
        if (_taskStack.TryPeek(out var currentTask) && currentTask.ID == entry.Task.ID) {
            _taskStack.Pop();
        }

        RenderTreeMessage($"[red]❌[/] [bold]{entry.Task.Name}[/] [red]failed[/]", "└─");

        // Create an ExceptionInfo from the log data to pass to the renderer.
        var exceptionInfo = new ExceptionInfo(new Exception(entry.Messager, new Exception(entry.StackTrace)));
        RenderExceptionPanel(exceptionInfo, $"Task Failed: {entry.Task.Name}");

        if (_taskTimers.ContainsKey(entry.Task.ID)) {
            _taskTimers.Remove(entry.Task.ID);
        }
    }

    private static void HandleScriptExecutionFailed(ScriptExecutionFailedLogEntry entry) {
        var exceptionInfo = new ExceptionInfo(new Exception(entry.ErrorMessage, new Exception(entry.StackTrace)));
        RenderExceptionPanel(exceptionInfo, $"Script Failure: {Path.GetFileName(entry.ScriptPath)}");
    }

    private static void HandleBasicPluginLog(BasicPluginLogEntry entry) {
        var message = Markup.Escape(entry.Message);
        string styledMessage;

        // Apply specific styling for known message patterns
        if (message.StartsWith("Using cached object file")) {
            styledMessage = $"[grey]{message}[/]";
        } else if (message.StartsWith("Resolving Dependency")) {
            styledMessage = $"[yellow]Resolving dependency[/] [bold]{message.Split('\'').ElementAtOrDefault(1) ?? ""}[/]";
        } else {
            styledMessage = $"[bold dim][[{entry.Plugin.Name}]][/] {message}";
        }
        RenderTreeMessage(styledMessage);
    }

    private static void HandleBasicLog(BasicLogEntry entry) {
        var color = entry.Level switch {
            LogLevel.Warning => "yellow",
            LogLevel.Error => "red",
            _ => "grey"
        };
        RenderTreeMessage($"[{color}]ℹ {Markup.Escape(entry.Message)}[/]");
    }

    private static void HandleScriptLog(ScriptLogEntry entry) {
        RenderTreeMessage($"[yellow]→[/] {Markup.Escape(entry.Message)}");
    }

    private static void HandleCommandStdOut(CommandStdOutLogEntry entry) {
        if (entry.Quiet || string.IsNullOrWhiteSpace(entry.Message)) return;
        RenderTreeMessage($"[grey]{Markup.Escape(entry.Message)}[/]", "  ");
    }

    private static void HandleCommandStdErr(CommandStdErrLogEntry entry) {
        if (entry.Quiet || string.IsNullOrWhiteSpace(entry.Message)) return;
        RenderTreeMessage($"[red]→ {Markup.Escape(entry.Message)}[/]", "  ");
    }

    // --- Helper Methods ---

    /// <summary>
    /// Renders a message with indentation based on the current task stack depth.
    /// </summary>
    private static void RenderTreeMessage(string markup, string prefix = "│") {
        var indent = new StringBuilder();
        var stackDepth = _taskStack.Count > 0 ? _taskStack.Count - 1 : 0;

        for (var i = 0; i < stackDepth; i++) {
            indent.Append("  [dim]│[/] ");
        }

        if (_taskStack.Count > 0) {
            indent.Append($"[dim]{prefix}[/] ");
        }

        AnsiConsole.MarkupLine(indent.ToString() + markup);
    }

    /// <summary>
    /// Renders a formatted panel for displaying exception details.
    /// </summary>
    private static void RenderExceptionPanel(ExceptionInfo ex, string title) {
        var stackTrace = string.IsNullOrEmpty(ex.StackTrace) || ex.StackTrace == "Empty Stack Trace"
            ? "[dim]No stack trace available.[/]"
            : Markup.Escape(ex.StackTrace);

        var content = new StringBuilder();
        content.AppendLine($"[white]{Markup.Escape(ex.Message)}[/]");
        content.AppendLine($"[dim]{stackTrace}[/]");

        var current = ex;
        while (current.CausedBy.Any()) {
            var cause = current.CausedBy.First();
            content.AppendLine("\n[yellow]Caused by:[/] " + Markup.Escape(cause.Type));
            content.AppendLine(Markup.Escape(cause.Message));
            stackTrace = string.IsNullOrEmpty(cause.StackTrace) || cause.StackTrace == "Empty Stack Trace"
                ? "[dim]No stack trace available.[/]"
                : Markup.Escape(cause.StackTrace);
            content.AppendLine($"[dim]{stackTrace}[/]");
            current = cause;
        }

        var panel = new Panel(content.ToString())
            .Header(title, Justify.Left)
            .Border(BoxBorder.Rounded)
            .BorderStyle(new Style(Color.Red))
            .Padding(1, 1);

        AnsiConsole.Write(panel);
    }
}
