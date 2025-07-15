using System.Text;
using System.Text.RegularExpressions;

namespace Shiron.Manila.Utils;

/// <summary>
/// A utility class for handling specific string formats using regular expressions.
/// </summary>
public static partial class RegexUtils {
    /// <summary>
    /// Regex for the format: [[project[/artifact]:]]task
    /// </summary>
    public static readonly Regex TaskRegex = TaskRegexGenerator();

    /// <summary>
    /// Represents a successful match for a task string.
    /// </summary>
    public record TaskMatch(string? Project, string? Artifact, string Task) {
        /// <summary>
        /// Reconstructs the string identifier from this TaskMatch object.
        /// </summary>
        /// <returns>The formatted task string.</returns>
        public string Format() {
            var builder = new StringBuilder();
            if (Project != null) {
                builder.Append(Project);
                if (Artifact != null) {
                    builder.Append('/').Append(Artifact);
                }
                builder.Append(':');
            }
            builder.Append(Task);
            return builder.ToString();
        }

        /// <summary>
        /// Returns a string representation of the TaskMatch object, including null parameters and the class name.
        /// </summary>
        public override string ToString() {
            return $"TaskMatch(Project: {Project ?? "null"}, Artifact: {Artifact ?? "null"}, Task: {Task})";
        }
    }

    /// <summary>
    /// Matches a string against the TaskRegex.
    /// </summary>
    /// <param name="s">The input string.</param>
    /// <returns>A <see cref="TaskMatch"/> record if the match is successful; otherwise, null.</returns>
    public static TaskMatch? MatchTasks(string s) {
        var match = TaskRegex.Match(s.Trim());
        if (!match.Success) return null;

        return new TaskMatch(
            GetValueOrNull(match.Groups["project"]),
            GetValueOrNull(match.Groups["artifact"]),
            match.Groups["task"].Value
        );
    }

    /// <summary>
    /// Checks if a string is a valid task identifier.
    /// </summary>
    /// <param name="s">The string to validate.</param>
    /// <returns>True if the string matches the task format; otherwise, false.</returns>
    public static bool IsValidTask(string s) => TaskRegex.IsMatch(s.Trim());

    [GeneratedRegex(@"^(?:(?<project>\w+)(?:\/(?<artifact>\w+))?:)?(?<task>\w+)$", RegexOptions.Compiled)]
    private static partial Regex TaskRegexGenerator();

    /// <summary>
    /// Regex for the format: [group:]plugin[@version]
    /// </summary>
    public static readonly Regex PluginRegex = PluginRegexGenerator();

    /// <summary>
    /// Represents a successful match for a plugin string.
    /// </summary>
    public record PluginMatch(string? Group, string Plugin, string? Version) {
        /// <summary>
        /// Reconstructs the string identifier from this PluginMatch object.
        /// </summary>
        /// <returns>The formatted plugin string.</returns>
        public string Format() {
            var builder = new StringBuilder();
            if (Group != null) {
                builder.Append(Group).Append(':');
            }
            builder.Append(Plugin);
            if (Version != null) {
                builder.Append('@').Append(Version);
            }
            return builder.ToString();
        }

        /// <summary>
        /// Returns a string representation of the PluginMatch object, including null parameters and the class name.
        /// </summary>
        public override string ToString() {
            return $"PluginMatch(Group: {Group ?? "null"}, Plugin: {Plugin}, Version: {Version ?? "null"})";
        }
    }

    /// <summary>
    /// Matches a string against the PluginRegex.
    /// </summary>
    /// <param name="s">The input string.</param>
    /// <returns>A <see cref="PluginMatch"/> record if the match is successful; otherwise, null.</returns>
    public static PluginMatch? MatchPlugin(string s) {
        var match = PluginRegex.Match(s.Trim());
        if (!match.Success) return null;

        return new PluginMatch(
            GetValueOrNull(match.Groups["group"]),
            match.Groups["plugin"].Value,
            GetValueOrNull(match.Groups["version"])
        );
    }

    /// <summary>
    /// Checks if a string is a valid plugin identifier.
    /// </summary>
    /// <param name="s">The string to validate.</param>
    /// <returns>True if the string matches the plugin format; otherwise, false.</returns>
    public static bool IsValidPlugin(string s) => PluginRegex.IsMatch(s.Trim());

    [GeneratedRegex(@"^(?:(?<group>[^:@/]+):)?(?<plugin>[^:@/]+)(?:@(?<version>\d+(?:\.\d+)*))?$", RegexOptions.Compiled)]
    private static partial Regex PluginRegexGenerator();

    /// <summary>
    /// Regex for the format: [group:]plugin[@version]:component
    /// </summary>
    public static readonly Regex PluginComponentRegex = PluginComponentRegexGenerator();

    /// <summary>
    /// Represents a successful match for a plugin component string.
    /// </summary>
    public record PluginComponentMatch(string? Group, string Plugin, string? Version, string Component) {
        /// <summary>
        /// Reconstructs the string identifier from this PluginComponentMatch record.
        /// </summary>
        public string Format() {
            var builder = new StringBuilder();
            if (Group != null) {
                builder.Append(Group).Append(':');
            }
            builder.Append(Plugin);
            if (Version != null) {
                builder.Append('@').Append(Version);
            }
            builder.Append('/').Append(Component);
            return builder.ToString();
        }

        /// <summary>
        /// Returns a string representation of the PluginComponentMatch object.
        /// </summary>
        public override string ToString() {
            return $"PluginComponentMatch(Group: {Group ?? "null"}, Plugin: {Plugin}, Version: {Version ?? "null"}, Component: {Component})";
        }
    }

    /// <summary>
    /// Matches a string against the PluginComponentRegex.
    /// </summary>
    /// <param name="s">The input string.</param>
    /// <returns>A <see cref="PluginComponentMatch"/> record if the match is successful; otherwise, null.</returns>
    public static PluginComponentMatch? MatchPluginComponent(string s) {
        var match = PluginComponentRegex.Match(s.Trim());
        if (!match.Success) return null;

        return new PluginComponentMatch(
            GetValueOrNull(match.Groups["group"]),
            match.Groups["plugin"].Value,
            GetValueOrNull(match.Groups["version"]),
            match.Groups["component"].Value
        );
    }

    /// <summary>
    /// Checks if a string is a valid plugin component identifier.
    /// </summary>
    public static bool IsValidPluginComponent(string s) => PluginComponentRegex.IsMatch(s.Trim()) && MatchPluginComponent(s.Trim())!.Component != null;

    [GeneratedRegex(@"^(?:(?<group>[^:@\/]+):)?(?<plugin>[^:@\/]+)(?:@(?<version>\d+(?:\.\d+)*))?\/(?<component>[\w-]+)$", RegexOptions.Compiled)]
    private static partial Regex PluginComponentRegexGenerator();

    /// <summary>
    /// Regex for the format: [group:]plugin[@version]/apiclass
    /// </summary>
    public static readonly Regex PluginApiClassRegex = PluginApiClassRegexGenerator();

    /// <summary>
    /// Represents a successful match for a plugin API class string.
    /// </summary>
    public record PluginApiClassMatch(string? Group, string Plugin, string? Version, string ApiClass) {
        /// <summary>
        /// Reconstructs the string identifier from this PluginApiClassMatch record.
        /// </summary>
        public string Format() {
            var builder = new StringBuilder();
            if (Group != null) {
                builder.Append(Group).Append(':');
            }
            builder.Append(Plugin);
            if (Version != null) {
                builder.Append('@').Append(Version);
            }
            builder.Append('/').Append(ApiClass);
            return builder.ToString();
        }

        /// <summary>
        /// Returns a string representation of the PluginApiClassMatch object.
        /// </summary>
        public override string ToString() {
            return $"PluginApiClassMatch(Group: {Group ?? "null"}, Plugin: {Plugin}, Version: {Version ?? "null"}, ApiClass: {ApiClass})";
        }
    }

    /// <summary>
    /// Matches a string against the PluginApiClassRegex.
    /// </summary>
    /// <param name="s">The input string.</param>
    /// <returns>A <see cref="PluginApiClassMatch"/> record if the match is successful; otherwise, null.</returns>
    public static PluginApiClassMatch? MatchPluginApiClass(string s) {
        var match = PluginApiClassRegex.Match(s.Trim());
        if (!match.Success) return null;

        return new PluginApiClassMatch(
            GetValueOrNull(match.Groups["group"]),
            match.Groups["plugin"].Value,
            GetValueOrNull(match.Groups["version"]),
            match.Groups["apiclass"].Value
        );
    }

    /// <summary>
    /// Checks if a string is a valid plugin API class identifier.
    /// </summary>
    public static bool IsValidPluginApiClass(string s) => PluginApiClassRegex.IsMatch(s.Trim());

    [GeneratedRegex(@"^(?:(?<group>[^:@\/]+):)?(?<plugin>[^:@\/]+)(?:@(?<version>\d+(?:\.\d+)*))?\/(?<apiclass>[\w-]+)$", RegexOptions.Compiled)]
    private static partial Regex PluginApiClassRegexGenerator();

    /// <summary>
    /// Helper to get a group's value, or null if the group was not captured.
    /// </summary>
    private static string? GetValueOrNull(Group group) => group.Success ? group.Value : null;
}
