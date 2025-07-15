using System.Text;
using System.Text.RegularExpressions;

namespace Shiron.Manila.Utils;

/// <summary>
/// A utility class for handling specific string formats using regular expressions.
/// </summary>
public static partial class RegexUtils {
    #region Task Regex

    /// <summary>
    /// Regex for the format: [[project[/artifact]:]]task
    /// </summary>
    public static readonly Regex TaskRegex = TaskRegexGenerator();

    /// <summary>
    /// Represents a successful match for a task string.
    /// </summary>
    /// <param name="project">The optional project name.</param>
    /// <param name="artifact">The optional artifact name.</param>
    /// <param name="task">The required task name.</param>
    public class TaskMatch(string? project, string? artifact, string task) {
        public string? Project { get; init; } = project;
        public string? Artifact { get; init; } = artifact;
        public string Task { get; init; } = task;

        public override string ToString() {
            return $"TaskMatch(Project = {Project ?? "null"}, Artifact = {Artifact ?? "null"}, Task = {Task})";
        }
    }

    /// <summary>
    /// Matches a string against the TaskRegex. Note: This method does not return null; it will return a TaskMatch with null properties for non-matching parts.
    /// </summary>
    /// <param name="s">The input string.</param>
    /// <returns>A TaskMatch object representing the parts of the string.</returns>
    public static TaskMatch MatchTasks(string s) {
        var res = TaskRegex.Match(s);

        // Use the Success property to check if a group was captured.
        return new TaskMatch(
            res.Groups["project"].Success ? res.Groups["project"].Value : null,
            res.Groups["artifact"].Success ? res.Groups["artifact"].Value : null,
            res.Groups["task"].Value
        );
    }

    /// <summary>
    /// Checks if a string is a valid task identifier.
    /// </summary>
    /// <param name="s">The string to validate.</param>
    /// <returns>True if the string matches the task format; otherwise, false.</returns>
    public static bool IsValidTask(string s) {
        return TaskRegex.IsMatch(s);
    }

    /// <summary>
    /// Reconstructs the string identifier from a TaskMatch object.
    /// </summary>
    /// <param name="match">The TaskMatch object.</param>
    /// <returns>The formatted task string.</returns>
    public static string FromTaskMatch(TaskMatch match) {
        if (match.Project == null) return match.Task;
        if (match.Artifact == null) return $"{match.Project}:{match.Task}";
        return $"{match.Project}/{match.Artifact}:{match.Task}";
    }

    [GeneratedRegex(@"^(?:(?<project>\w+)(?:\/(?<artifact>\w+))?:)?(?<task>\w+)$", RegexOptions.Compiled)]
    private static partial Regex TaskRegexGenerator();

    #endregion

    #region Plugin Component Regex

    /// <summary>
    /// Regex for the format: [group:]<plugin>[@version]<:component>
    /// </summary>
    public static readonly Regex PluginComponentRegex = PluginComponentRegexGenerator();

    /// <summary>
    /// Represents a successful match for a plugin component string.
    /// </summary>
    public record PluginComponentMatch(string? Group, string Plugin, string? Version, string Component);

    /// <summary>
    /// Matches a string against the PluginComponentRegex.
    /// </summary>
    /// <param name="s">The input string.</param>
    /// <returns>A PluginComponentMatch record if successful, otherwise null.</returns>
    public static PluginComponentMatch? MatchPluginComponent(string s) {
        var match = PluginComponentRegex.Match(s);
        if (!match.Success) return null;

        return new PluginComponentMatch(
            match.Groups["group"].Success ? match.Groups["group"].Value : null,
            match.Groups["plugin"].Value,
            match.Groups["version"].Success ? match.Groups["version"].Value : null,
            match.Groups["component"].Value
        );
    }

    /// <summary>
    /// Checks if a string is a valid plugin component identifier.
    /// </summary>
    public static bool IsValidPluginComponent(string s) {
        return PluginComponentRegex.IsMatch(s);
    }

    /// <summary>
    /// Reconstructs the string identifier from a PluginComponentMatch record.
    /// </summary>
    public static string FromPluginComponentMatch(PluginComponentMatch match) {
        var builder = new StringBuilder();
        if (match.Group != null) {
            builder.Append(match.Group).Append(':');
        }
        builder.Append(match.Plugin);
        if (match.Version != null) {
            builder.Append('@').Append(match.Version);
        }
        builder.Append(':').Append(match.Component);
        return builder.ToString();
    }

    [GeneratedRegex(@"^(?:(?<group>[\w-]+):)?(?<plugin>[\w-]+)(?:@(?<version>[\d\.]+))?:(?<component>[\w-]+)$", RegexOptions.Compiled)]
    private static partial Regex PluginComponentRegexGenerator();

    #endregion

    #region Plugin ApiClass Regex

    /// <summary>
    /// Regex for the format: [group:]<plugin>[@version]</apiclass>
    /// </summary>
    public static readonly Regex PluginApiClassRegex = PluginApiClassRegexGenerator();

    /// <summary>
    /// Represents a successful match for a plugin API class string.
    /// </summary>
    public record PluginApiClassMatch(string? Group, string Plugin, string? Version, string ApiClass);

    /// <summary>
    /// Matches a string against the PluginApiClassRegex.
    /// </summary>
    /// <param name="s">The input string.</param>
    /// <returns>A PluginApiClassMatch record if successful, otherwise null.</returns>
    public static PluginApiClassMatch? MatchPluginApiClass(string s) {
        var match = PluginApiClassRegex.Match(s);
        if (!match.Success) return null;

        return new PluginApiClassMatch(
            match.Groups["group"].Success ? match.Groups["group"].Value : null,
            match.Groups["plugin"].Value,
            match.Groups["version"].Success ? match.Groups["version"].Value : null,
            match.Groups["apiclass"].Value
        );
    }

    /// <summary>
    /// Checks if a string is a valid plugin API class identifier.
    /// </summary>
    public static bool IsValidPluginApiClass(string s) {
        return PluginApiClassRegex.IsMatch(s);
    }

    /// <summary>
    /// Reconstructs the string identifier from a PluginApiClassMatch record.
    /// </summary>
    public static string FromPluginApiClassMatch(PluginApiClassMatch match) {
        var builder = new StringBuilder();
        if (match.Group != null) {
            builder.Append(match.Group).Append(':');
        }
        builder.Append(match.Plugin);
        if (match.Version != null) {
            builder.Append('@').Append(match.Version);
        }
        builder.Append("</").Append(match.ApiClass).Append('>');
        return builder.ToString();
    }

    [GeneratedRegex(@"^(?:(?<group>[\w-]+):)?(?<plugin>[\w-]+)(?:@(?<version>[\d\.]+))?<\/(?<apiclass>[\w-]+)>$", RegexOptions.Compiled)]
    private static partial Regex PluginApiClassRegexGenerator();

    #endregion
}
