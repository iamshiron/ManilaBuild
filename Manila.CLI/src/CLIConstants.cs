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
        public static readonly string JobIdentifier = "[bold cyan]{0}[/]";
        public static readonly string Dependencies = "[italic]{0}[/]";
    }

    // Table column headers
    public static class TableColumns {
        public static readonly string Job = "[cyan]Job[/]";
        public static readonly string Description = "[green]Description[/]";
        public static readonly string Dependencies = "[magenta]Direct Dependencies[/]";
        public static readonly string Project = "[cyan]Project[/]";
        public static readonly string Plugin = "[cyan]Plugin[/]";
        public static readonly string Version = "[green]Version[/]";
        public static readonly string Artifacts = "[blue]Artifacts[/]";
        public static readonly string Group = "[magenta]Group[/]";
        public static readonly string Path = "[yellow]Path[/]";
        public static readonly string Author = "[red]Author[/]";
        public static readonly string Artifact = "[cyan]Artifact[/]";
        public static readonly string Template = "[magenta]Template[/]";
    }

    // Common messages and titles
    public static class Messages {
        public static readonly string NoWorkspace = "Not inside a workspace. Please run 'manila init' to create a new workspace.";
        public static readonly string AlreadyInitialized = "Workspace already initialized. Use 'manila init --force' to overwrite the current data directory.";
        public static readonly string CreatingNewWorkspace = "Creating new workspace...";
        public static readonly string WorkspaceInitialized = "Workspace initialized successfully!";
        public static readonly string ManilaEngineNotInitialized = "Manila engine is not initialized. Please run 'manila init' first.";
        public static readonly string AvailableJobs = "Available Jobs";
        public static readonly string AvailableProjects = "Available Projects";
        public static readonly string AvailablePlugins = "Available Plugins";
        public static readonly string WorkspaceJobs = "Workspace Jobs";

        public static readonly string ProjectCreatedSuccessfully = "[green]Project created successfully![/]";

        public static class InvalidSubCommand {
            public static readonly string ApiSubcommand = "Invalid or unknown API subcommand! Run 'manila api --help' to see available subcommands.";
        }
    }

    // Border styles
    public static class BorderStyles {
        public static readonly string Default = "grey";
        public static readonly string TableBorder = "rounded";
    }

    // API subcommands
    public static class ApiSubcommands {
        public static readonly string Jobs = "jobs";
        public static readonly string Artifacts = "artifacts";
        public static readonly string Projects = "projects";
        public static readonly string Workspace = "workspace";
        public static readonly string Plugins = "plugins";

        // Returns an array of all valid API subcommands
        public static string[] All => [Jobs, Artifacts, Projects, Workspace, Plugins];
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
            @"[blue]|_|  |_|\__,_|_| |_|_|_|\__,_|[/] [magenta]v{0}[/]"
        ];
    }

    public static string[] IgnoreBannerOnCommands => [
        "api"
    ];

    public static class ScriptDefaults {
        public static readonly string[] WorkspaceScript = [
            "var workspace = Manila.GetWorkspace();",
            "",
            "Manila.Job(\"hello-world\")",
            "    .Description(\"A simple hello world job\")",
            "    .Execute(() => {",
            "        Manila.Log(\"Hello World from the workspace script!\");",
            "    });",
            "",
        ];
    }
}
