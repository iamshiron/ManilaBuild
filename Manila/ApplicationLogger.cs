using Shiron.Manila.API;
using Spectre.Console;

namespace Shiron.Manila;

/// <summary>
/// Used for printing progress and giving feedback to the user.
/// This is not meant for debugging or logging purposes. Use the <see cref="Logger"/> class for more details about debug logs.
/// </summary>
public static class ApplicationLogger {
    private static bool _stackTraceEnabled = false;
    private static bool _quiet = false;

    private static long _buildStartTime = 0;
    private static TaskInfo? _runningTask = null;

    public record TaskInfo {
        public required API.Task Task { get; init; }
        public long StartTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        public List<TaskInfo> SubTasks = [];
    }

    public static void Init(bool quiet, bool stackTraceEnabled) {
        _quiet = quiet;
        _stackTraceEnabled = stackTraceEnabled;
    }

    public static void BuildStarted() {
        if (_buildStartTime != 0) throw new InvalidOperationException("Build already started.");
        _buildStartTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        AnsiConsole.MarkupLine($"[blue]━━━━━━━━━━━━━━━━━━━━━━━━━━ Build Process Started ━━━━━━━━━━━━━━━━━━━━━━━━━━[/]");
    }
    public static void TaskStarted(API.Task task) {
        var info = new TaskInfo { Task = task, };
        if (_runningTask == null) _runningTask = info;
        else _runningTask.SubTasks.Add(info);

        AnsiConsole.MarkupLine($"[blue]→[/] Task: {task.name} [grey]({(task.Component is Workspace ? "Workspace" : (task.Component as Project)!.Name)})[/]");
    }
    public static void TaskFinished() {
        if (_runningTask == null) throw new InvalidOperationException("No task is running.");
        TaskInfo current = _runningTask;
        var duration = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - current.StartTime;

        while (current.SubTasks.Count > 0) {
            current = current.SubTasks[^1];
        }

        AnsiConsole.MarkupLine($"[green]SUCCESS[/] [bold]{_runningTask?.Task.name}[/] completed in {Math.Round(duration / 1000f, 1)}s");
    }
    public static void BuildFinished(Exception? e = null) {
        var duration = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - _buildStartTime;

        if (e == null) {
            AnsiConsole.MarkupLine($"[green]━━━━━━━━━━━━━━━━━━━━━━━━━━ Build Successful ━━━━━━━━━━━━━━━━━━━━━━━━━━[/]");
        } else {
            AnsiConsole.MarkupLine($"[red]FAILED[/] [bold]{_runningTask?.Task.name}[/] failed in {Math.Round(duration / 1000f, 1)}s");

            AnsiConsole.MarkupLine($"[red]━━━━━━━━━━━━━━━━━━━━━━━━━━ Build Failed ━━━━━━━━━━━━━━━━━━━━━━━━━━[/]");
            if (_stackTraceEnabled) {
                var panel = new Panel(Markup.Escape(e.StackTrace) ?? "No stack trace available") {
                    Border = BoxBorder.Rounded,
                    BorderStyle = Style.Parse("red dim"),
                    Header = new PanelHeader($"{e.GetType().Name}: {e.Message}") {
                    },
                    Padding = new Padding(0),
                };
                AnsiConsole.Write(panel);
            }
        }
    }
}
