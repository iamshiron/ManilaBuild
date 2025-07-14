
using System.Text.RegularExpressions;

namespace Shiron.Manila.Utils;

public static partial class RegexUtils {
    public static readonly Regex TaskRegex = TaskRegexGenerator();

    public class TaskMatch(string? project, string? artifact, string task) {
        public string? Project { get; init; } = project;
        public string? Artifact { get; init; } = artifact;
        public string Task { get; init; } = task;

        public override string ToString() {
            return $"TaskMatch(Project = {Project ?? "null"}, Artifact = {Artifact ?? "null"}, Task = {Task})";
        }
    }

    public static TaskMatch MatchTasks(string s) {
        var res = TaskRegex.Match(s);

        return new TaskMatch(
            res.Groups["project"].Value == string.Empty ? null : res.Groups["project"].Value,
            res.Groups["artifact"].Value == string.Empty ? null : res.Groups["artifact"].Value,
            res.Groups["task"].Value
        );
    }

    public static bool IsValidTaskRegex(string s) {
        return TaskRegex.IsMatch(s);
    }

    public static string FromTaskMatch(TaskMatch match) {
        if (match.Project == null) return match.Task;
        if (match.Artifact == null) return $"{match.Project}:{match.Task}";
        return $"{match.Project}/{match.Artifact}:{match.Task}";
    }

    [GeneratedRegex(@"^(?:(?<project>\w+)(?:\/(?<artifact>\w+))?:)?(?<task>\w+)$", RegexOptions.Compiled)]
    private static partial Regex TaskRegexGenerator();
}
