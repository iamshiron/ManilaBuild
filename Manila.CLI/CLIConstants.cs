using System;

namespace Shiron.Manila.CLI;

/// <summary>
/// Constants used throughout the Manila CLI application to avoid magic strings.
/// </summary>
public static class CLIConstants {
    // Common string formats and patterns
    public static class Format {
        public static readonly string Rule = "[bold yellow]{0}[/]";
        public static readonly string Header = "[bold blue]{0}[/]";
        public static readonly string SubHeader = "[bold yellow]{0}[/]";
        public static readonly string TaskIdentifier = "[bold cyan]{0}[/]";
        public static readonly string Dependencies = "[italic]{0}[/]";
    }

    // Table column headers
    public static class TableColumns {
        public static readonly string Task = "[cyan]Task[/]";
        public static readonly string Description = "[green]Description[/]";
        public static readonly string Dependencies = "[magenta]Direct Dependencies[/]";
        public static readonly string Project = "[cyan]Project[/]";
        public static readonly string Version = "[green]Version[/]";
        public static readonly string Artifacts = "[blue]Artifacts[/]";
        public static readonly string Group = "[magenta]Group[/]";
        public static readonly string Path = "[yellow]Path[/]";
        public static readonly string Author = "[red]Author[/]";
        public static readonly string Artifact = "[cyan]Artifact[/]";
    }

    // Common messages and titles
    public static class Messages {
        public static readonly string NoWorkspace = "Not inside a workspace";
        public static readonly string AvailableTasks = "Available Tasks";
        public static readonly string AvailableProjects = "Available Projects";
        public static readonly string AvailablePlugins = "Available Plugins";
        public static readonly string WorkspaceTasks = "Workspace Tasks";
    }

    // Border styles
    public static class BorderStyles {
        public static readonly string Default = "grey";
        public static readonly string TableBorder = "rounded";
    }

    // API subcommands
    public static class ApiSubcommands {
        public static readonly string Tasks = "tasks";
        public static readonly string Artifacts = "artifacts";
        public static readonly string Projects = "projects";
        public static readonly string Workspace = "workspace";
        public static readonly string Plugins = "plugins";

        // Returns an array of all valid API subcommands
        public static string[] All => [Tasks, Artifacts, Projects, Workspace, Plugins];
    }

    // Command line options
    public static class CommandOptions {
        public static readonly string Quiet = "--quiet";
        public static readonly string QuietShort = "-q";
        public static readonly string Verbose = "--verbose";
        public static readonly string Structured = "--structured";
        public static readonly string Json = "--json";
        public static readonly string StackTrace = "--stack-trace";
        public static readonly string Project = "--project";
        public static readonly string Detailed = "--detailed";
    }

    // Project type identifiers
    public static class ProjectTypes {
        public static readonly string Workspace = "workspace";
        public static readonly string Project = "project";
        public static readonly string Artifact = "artifact";
    }

    // ASCII art banner for Manila
    public static class Banner {
        public static readonly string[] Lines = [
            @"[blue] __  __             _ _[/]",
            @"[blue]|  \/  | __ _ _ __ (_| | __ _[/]",
            @"[blue]| |\/| |/ _` | '_ \| | |/ _` |[/]",
            @"[blue]| |  | | (_| | | | | | | (_| |[/]",
            @"[blue]|_|  |_|\\__,_|_| |_|_|_|\\__,_|[/] [magenta]v{0}[/]"
        ];
    }

    // Directory paths
    public static class Directories {
        public static readonly string Plugins = ".manila/plugins";
        public static readonly string Nuget = ".manila/nuget";
        public static readonly string Profiles = ".manila/profiles";
    }
}
