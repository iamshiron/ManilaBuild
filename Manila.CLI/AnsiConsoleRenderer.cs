using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Shiron.Manila.Logging;
using Spectre.Console;
using System.Diagnostics;

/// <summary>
/// Handles rendering structured log entries into a human-readable format
/// using Spectre.Console, creating a tree-like view of the build process.
/// </summary>
public static class AnsiConsoleRenderer {
    // A stack to keep track of running tasks for proper indentation and hierarchy.
    private static readonly Stack<TaskInfo> _taskStack = new();
    // A dictionary to store the start time of tasks to calculate duration.
    private static readonly Dictionary<string, long> _taskTimers = new();

    /// <summary>
    /// Initializes the console renderer, subscribing to the logger's OnLogEntry event.
    /// </summary>
    /// <param name="quiet">If true, only logs errors and above.</param>
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
                // Output raw JSON if structured logging is enabled
                Console.WriteLine(JsonConvert.SerializeObject(entry, jsonSerializerSettings));
                return;
            }

            // In quiet mode, only render errors.
            if (quiet && entry.Level < LogLevel.Error) {
                return;
            }

            // For standard output, filter out noisy logs.
            if (entry.Level < LogLevel.Info) {
                return;
            }

            RenderLog(entry);
        };
    }

    /// <summary>
    /// Renders a single log entry to the console with appropriate formatting.
    /// </summary>
    /// <param name="e">The log entry to render.</param>
    private static void RenderLog(ILogEntry e) {
        // The core logic to render different log types.
        switch (e) {
            // --- Build Lifecycle Events ---
            case ProjectsInitializedLogEntry entry:
                AnsiConsole.MarkupLine($"[white]Initialization took:[/] [green]{entry.Duration}ms[/]\n");
                break;

            case BuildStartedLogEntry:
                AnsiConsole.MarkupLine("[green]▶[/] [bold]Executing build...[/]");
                break;

            case BuildCompletedLogEntry entry:
                AnsiConsole.MarkupLine($"\n[green]BUILD SUCCESSFUL[/] in {entry.Duration}ms");
                break;

            case BuildFailedLogEntry entry:
                AnsiConsole.MarkupLine($"\n[red]BUILD FAILED![/] after {entry.Duration}ms");
                // In a real implementation, you would render the exception details here.
                break;

            // --- Task Execution Events ---
            case TaskExecutionStartedLogEntry entry:
                // When a task starts, push it to the stack and record its start time.
                var taskIdentifier = $"{entry.Task.Name}:{entry.ContextID}";
                _taskStack.Push(entry.Task);
                _taskTimers[taskIdentifier] = Stopwatch.GetTimestamp();
                RenderTreeMessage($"[cyan]{entry.Task.Name}[/]");
                break;

            case TaskExecutionFinishedLogEntry entry:
                // When a task finishes, pop it and calculate the duration.
                var finishedTaskIdentifier = $"{entry.Task.Name}:{entry.ContextID}";
                if (_taskStack.Count > 0) _taskStack.Pop();
                if (_taskTimers.TryGetValue(finishedTaskIdentifier, out var startTime)) {
                    var duration = Stopwatch.GetElapsedTime(startTime);
                    RenderTreeMessage($"[green]✓[/] [bold]Task {entry.Task.Name} completed[/] [dim]in {duration.TotalMilliseconds:F0}ms[/]");
                }
                break;

            // --- Plugin & Script Messages ---
            case BasicPluginLogEntry entry:
                // Render messages that come from specific plugins.
                var message = entry.Message;
                if (message.StartsWith("Using cached object file")) {
                    // Special handling for cache messages to make them less prominent.
                    RenderTreeMessage($"[grey]{Markup.Escape(message)}[/]", "├─");
                } else if (message.StartsWith("Resolving Dependency")) {
                    RenderTreeMessage($"[yellow]Resolving dependency[/] [bold]{message.Split('\'')[1]}[/]", "├─");
                } else {
                    RenderTreeMessage($"[bold]{Markup.Escape(entry.Plugin.Name)}[/]: {Markup.Escape(message)}");
                }
                break;

            case ScriptLogEntry entry:
                // Render logs that come directly from user scripts (e.g., print()).
                RenderTreeMessage($"[yellow]→[/] {Markup.Escape(entry.Message)}");
                break;

            // --- Command Output Events ---
            case CommandStdOutLogEntry entry:
                if (entry.Quiet) break;

                // Render standard output from commands in a neutral color.
                RenderTreeMessage($"[grey]{Markup.Escape(entry.Message)}[/]", "  ");
                break;

            case CommandStdErrLogEntry entry:
                if (entry.Quiet) break;

                // Render standard error from commands in a distinct error color.
                RenderTreeMessage($"[red]→ {Markup.Escape(entry.Message)}[/]", "  ");
                break;
        }
    }

    /// <summary>
    /// Helper method to render a message with the correct indentation based on the task stack.
    /// </summary>
    /// <param name="message">The message to render.</param>
    /// <param name="prefix">The prefix for the current line (e.g., ├─ or └─).</param>
    private static void RenderTreeMessage(string message, string prefix = "│") {
        var indent = new System.Text.StringBuilder();
        // Create an indent string based on the depth of the task stack.
        for (int i = 0; i < _taskStack.Count; i++) {
            indent.Append("  [dim]│[/]  ");
        }

        if (_taskStack.Count > 0) {
            indent.Append($"[dim]{prefix}[/] ");
        }

        AnsiConsole.MarkupLine(indent.ToString() + message);
    }
}
