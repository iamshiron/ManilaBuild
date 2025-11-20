using System.Text;
using System.Text.RegularExpressions;

namespace Shiron.Manila.API.Utils;

/// <summary>Regex helpers for DSL identifiers.</summary>
public static partial class RegexUtils {
    /// <summary>
    /// Regex for the format: [[project[/artifact]:]]job
    /// </summary>
    public static readonly Regex JobRegex = JobRegexGenerator();

    /// <summary>Matched job identifier.</summary>
    public record JobMatch(string? Project, string? Artifact, string Job) {
        /// <summary>Format canonical string.</summary>
        /// <returns>Lower-cased job spec.</returns>
        public string Format() {
            var builder = new StringBuilder();
            if (Project != null) {
                _ = builder.Append(Project);
                if (Artifact != null) {
                    _ = builder.Append('/').Append(Artifact);
                }
                _ = builder.Append(':');
            }
            _ = builder.Append(Job);
            return builder.ToString().ToLower();
        }

        /// <summary>Debug string.</summary>
        public override string ToString() {
            return $"JobMatch(Project: {Project ?? "null"}, Artifact: {Artifact ?? "null"}, Job: {Job})";
        }
    }

    /// <summary>Try match job spec.</summary>
    /// <param name="s">Input string.</param>
    /// <returns>Match or null.</returns>
    public static JobMatch? MatchJobs(string s) {
        var match = JobRegex.Match(s.Trim());
        if (!match.Success) return null;

        return new JobMatch(
            GetValueOrNull(match.Groups["project"]),
            GetValueOrNull(match.Groups["artifact"]),
            match.Groups["job"].Value
        );
    }

    /// <summary>Validate job spec.</summary>
    /// <param name="s">Input.</param>
    /// <returns>True if valid.</returns>
    public static bool IsValidJob(string s) => JobRegex.IsMatch(s.Trim());

    [GeneratedRegex(@"^(?:(?<project>\w+)(?:\/(?<artifact>\w+))?:)?(?<job>\w+)$", RegexOptions.Compiled)]
    private static partial Regex JobRegexGenerator();

    /// <summary>
    /// Regex for the format: [group:]plugin[@version]
    /// </summary>
    public static readonly Regex PluginRegex = PluginRegexGenerator();

    /// <summary>Matched plugin identifier.</summary>
    public record PluginMatch(string? Group, string Plugin, string? Version) {
        /// <summary>Format canonical string.</summary>
        /// <returns>Plugin spec.</returns>
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

        /// <summary>Debug string.</summary>
        public override string ToString() {
            return $"PluginMatch(Group: {Group ?? "null"}, Plugin: {Plugin}, Version: {Version ?? "null"})";
        }
    }

    /// <summary>Try match plugin spec.</summary>
    /// <param name="s">Input string.</param>
    /// <returns>Match or null.</returns>
    public static PluginMatch? MatchPlugin(string s) {
        var match = PluginRegex.Match(s.Trim());
        if (!match.Success) return null;

        return new PluginMatch(
            GetValueOrNull(match.Groups["group"]),
            match.Groups["plugin"].Value,
            GetValueOrNull(match.Groups["version"])
        );
    }

    /// <summary>Validate plugin spec.</summary>
    /// <param name="s">Input.</param>
    /// <returns>True if valid.</returns>
    public static bool IsValidPlugin(string s) => PluginRegex.IsMatch(s.Trim());

    [GeneratedRegex(@"^(?:(?<group>[^:@/]+):)?(?<plugin>[^:@/]+)(?:@(?<version>\d+(?:\.\d+)*))?$", RegexOptions.Compiled)]
    private static partial Regex PluginRegexGenerator();

    /// <summary>
    /// Regex for the format: [group:]plugin[@version]:component
    /// </summary>
    public static readonly Regex PluginComponentRegex = PluginComponentRegexGenerator();

    /// <summary>Matched plugin component.</summary>
    public record PluginComponentMatch(string? Group, string Plugin, string? Version, string Component) {
        /// <summary>Format canonical string.</summary>
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

        /// <summary>Convert to plugin match.</summary>
        public PluginMatch ToPluginMatch() {
            return new PluginMatch(Group, Plugin, Version);
        }

        /// <summary>Debug string.</summary>
        public override string ToString() {
            return $"PluginComponentMatch(Group: {Group ?? "null"}, Plugin: {Plugin}, Version: {Version ?? "null"}, Component: {Component})";
        }
    }

    /// <summary>Try match plugin component.</summary>
    /// <param name="s">Input string.</param>
    /// <returns>Match or null.</returns>
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

    /// <summary>Validate plugin component.</summary>
    public static bool IsValidPluginComponent(string s) => PluginComponentRegex.IsMatch(s.Trim()) && MatchPluginComponent(s.Trim())!.Component != null;

    [GeneratedRegex(@"^(?:(?<group>[^:@\/]+):)?(?<plugin>[^:@\/]+)(?:@(?<version>\d+(?:\.\d+)*))?\/(?<component>[\w-]+)$", RegexOptions.Compiled)]
    private static partial Regex PluginComponentRegexGenerator();

    /// <summary>
    /// Regex for the format: [group:]plugin[@version]/apiclass
    /// </summary>
    public static readonly Regex PluginApiClassRegex = PluginApiClassRegexGenerator();

    /// <summary>Matched plugin API class.</summary>
    public record PluginApiClassMatch(string? Group, string Plugin, string? Version, string ApiClass) {
        /// <summary>Format canonical string.</summary>
        public string Format() {
            var builder = new StringBuilder();
            if (Group != null) {
                _ = builder.Append(Group).Append(':');
            }
            _ = builder.Append(Plugin);
            if (Version != null) {
                _ = builder.Append('@').Append(Version);
            }
            _ = builder.Append('/').Append(ApiClass);
            return builder.ToString();
        }

        public PluginMatch ToPluginMatch() {
            return new PluginMatch(Group, Plugin, Version);
        }

        /// <summary>Debug string.</summary>
        public override string ToString() {
            return $"PluginApiClassMatch(Group: {Group ?? "null"}, Plugin: {Plugin}, Version: {Version ?? "null"}, ApiClass: {ApiClass})";
        }
    }

    /// <summary>Try match plugin API class.</summary>
    /// <param name="s">Input string.</param>
    /// <returns>Match or null.</returns>
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

    /// <summary>Validate plugin API class.</summary>
    public static bool IsValidPluginApiClass(string s) => PluginApiClassRegex.IsMatch(s.Trim());

    [GeneratedRegex(@"^(?:(?<group>[^:@\/]+):)?(?<plugin>[^:@\/]+)(?:@(?<version>\d+(?:\.\d+)*))?\/(?<apiclass>[\w-]+)$", RegexOptions.Compiled)]
    private static partial Regex PluginApiClassRegexGenerator();

    /// <summary>
    /// Regex for the format: plugin:template
    /// </summary>
    public static readonly Regex TemplateRegex = TemplateRegexGenerator();

    /// <summary>Matched template identifier.</summary>
    public record TemplateMatch(string Plugin, string Template) {
        /// <summary>Format canonical string.</summary>
        /// <returns>Template spec.</returns>
        public string Format() {
            return $"{Plugin}:{Template}";
        }

        /// <summary>Debug string.</summary>
        public override string ToString() {
            return $"TemplateMatch(Plugin: {Plugin}, Template: {Template})";
        }
    }

    /// <summary>Try match template spec.</summary>
    /// <param name="s">Input string.</param>
    /// <returns>Match or null.</returns>
    public static TemplateMatch? MatchTemplate(string s) {
        var match = TemplateRegex.Match(s.Trim());
        if (!match.Success) return null;

        return new TemplateMatch(
            match.Groups["plugin"].Value,
            match.Groups["template"].Value
        );
    }

    /// <summary>Validate template spec.</summary>
    /// <param name="s">Input.</param>
    /// <returns>True if valid.</returns>
    public static bool IsValidTemplate(string s) => TemplateRegex.IsMatch(s.Trim());

    [GeneratedRegex(@"^(?<plugin>[^:@/]+):(?<template>[\w-]+)$", RegexOptions.Compiled)]
    private static partial Regex TemplateRegexGenerator();

    /// <summary>Group value or null.</summary>
    private static string? GetValueOrNull(Group group) => group.Success ? group.Value : null;
}
