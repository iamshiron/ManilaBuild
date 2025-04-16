using System.Text;
using Microsoft.ClearScript;
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
    private static Stack<TaskInfo> _runningTask = new();

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
        WriteLine($"[blue]→[/] Task: {task.name} [grey]({(task.Component is Workspace ? "Workspace" : (task.Component as Project)!.Name)})[/]");
        _runningTask.Push(info);
    }
    public static void TaskFinished() {
        if (_runningTask == null) throw new InvalidOperationException("No task is running.");
        var task = _runningTask.Pop();
        var duration = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - task.StartTime;

        WriteLine($"[green]SUCCESS[/] [bold]{task.Task.name}[/] completed in {Math.Round(duration / 1000f, 1)}s");
    }
    public static void BuildFinished(Exception? e = null) {
        var duration = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - _buildStartTime;

        if (e == null) {
            AnsiConsole.MarkupLine($"[green]━━━━━━━━━━━━━━━━━━━━━━━━━━ Build Successful ━━━━━━━━━━━━━━━━━━━━━━━━━━[/]");
        } else {
            if (_runningTask.Count != 0) {
                var task = _runningTask.Peek();
                AnsiConsole.MarkupLine($"[red]FAILED[/] [bold]{task.Task.name}[/] failed in {Math.Round(duration / 1000f, 1)}s");
            } else {
                AnsiConsole.MarkupLine($"[red]FAILED[/] in {Math.Round(duration / 1000f, 1)}s");
            }

            if (_stackTraceEnabled) {
                AnsiConsole.MarkupLine(FormatException(e));
            }
        }
    }

    public static string FormatException(Exception e) {
        var builder = new StringBuilder();
        var script = Path.GetRelativePath(ManilaEngine.GetInstance().Root, _runningTask.Peek().Task.ScriptPath);

        if (e is ScriptEngineException ex) {
            builder.AppendLine($"[bold]{Markup.Escape(e.GetType().Name)}[/] [grey]({Markup.Escape(e.Message)})[/] \n" + $"[dim]{Markup.Escape(e.StackTrace) + "\n" + ex.ErrorDetails.Replace(" at Script:", "at " + script + ":")}[/]");
        } else {
            builder.AppendLine($"[bold]{Markup.Escape(e.GetType().Name)}[/] [grey]({Markup.Escape(e.Message)})[/] \n" + $"[dim]{Markup.Escape(e.StackTrace)}[/]");
        }

        if (e.InnerException != null) {
            builder.AppendLine($"[red]Caused By:[/] " + FormatException(e.InnerException));
        }

        return builder.ToString();
    }

    public static void WriteLine(params object[] messages) {
        if (_quiet) return;
        AnsiConsole.MarkupLine(new string(' ', _runningTask.Count * 2) + string.Join(" ", messages));
    }

    public static void ScriptLog(params object[] messages) {
        WriteLine($"[grey]→[/] {string.Join(" ", messages)}");
    }
    public static void ApplicationLog(params object[] message) {
        WriteLine($"[grey]→[/] {string.Join(" ", message)}");
    }
}
