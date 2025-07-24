using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Shiron.Manila.API;
using Shiron.Manila.Enums;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Ext;
using Shiron.Manila.Utils;

namespace Shiron.Manila.Logging;

#region Log Data Transfer Objects

/// <summary>
/// Represents a snapshot of a plugin's information for logging.
/// </summary>
public sealed class PluginInfo {
    public string Name { get; init; }
    public string Group { get; init; }
    public string Version { get; init; }
    public string[] Authors { get; init; }
    public string Entry { get; init; }
    public string[] NuGetDependencies { get; init; }
    public string File { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginInfo"/> class from a plugin instance.
    /// </summary>
    public PluginInfo(ManilaPlugin plugin) {
        Name = plugin.Name;
        Group = plugin.Group;
        Version = plugin.Version;
        Authors = plugin.Authors.ToArray();
        Entry = plugin.GetType().FullName!;
        NuGetDependencies = plugin.NugetDependencies.ToArray();
        File = plugin.File!;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginInfo"/> class with specified properties.
    /// </summary>
    [JsonConstructor]
    public PluginInfo(string name, string group, string version, string[] authors, string entry, string[] nuGetDependencies, string file) {
        Name = name;
        Group = group;
        Version = version;
        Authors = authors;
        Entry = entry;
        NuGetDependencies = nuGetDependencies;
        File = file;
    }
}


/// <summary>
/// Represents a snapshot of a job's information for logging.
/// </summary>
public sealed class JobInfo {
    public string Name { get; init; }
    public string ID { get; init; }
    public string ScriptPath { get; init; }
    public string Description { get; init; }
    public ComponentInfo Component { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="JobInfo"/> class from a job instance.
    /// </summary>
    public JobInfo(Job job) {
        Name = job.Name;
        ID = job.GetIdentifier();
        ScriptPath = job.Context.ScriptPath;
        Description = job.Description;
        Component = new ComponentInfo(job.Component);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JobInfo"/> class with specified properties.
    /// </summary>
    [JsonConstructor]
    public JobInfo(string name, string id, string scriptPath, string description, ComponentInfo component) {
        Name = name;
        ID = id;
        ScriptPath = scriptPath;
        Description = description;
        Component = component;
    }
}

/// <summary>
/// Represents a snapshot of a component's information for logging.
/// </summary>
public sealed class ComponentInfo {
    public bool IsProject { get; init; }
    public bool IsWorkspace { get; init; }
    public string Root { get; init; }
    public string ID { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ComponentInfo"/> class from a component instance.
    /// </summary>
    public ComponentInfo(Component component) {
        IsProject = component is Project;
        IsWorkspace = component is Workspace;
        Root = component.Path.Get();
        ID = component.GetIdentifier();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ComponentInfo"/> class with specified properties.
    /// </summary>
    [JsonConstructor]
    public ComponentInfo(bool isProject, bool isWorkspace, string root, string id) {
        IsProject = isProject;
        IsWorkspace = isWorkspace;
        Root = root;
        ID = id;
    }
}

/// <summary>
/// Represents a snapshot of a project's information for logging.
/// </summary>
public sealed class ProjectInfo {
    public string Name { get; init; }
    public string Identifier { get; init; }
    public string? Version { get; init; }
    public string? Group { get; init; }
    public string? Description { get; init; }
    public string Root { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectInfo"/> class from a project instance.
    /// </summary>
    public ProjectInfo(Project project) {
        Name = project.Name;
        Identifier = project.GetIdentifier();
        Version = project.Version;
        Group = project.Group;
        Description = project.Description;
        Root = project.Path.Get();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectInfo"/> class with specified properties.
    /// </summary>
    [JsonConstructor]
    public ProjectInfo(string name, string identifier, string? version, string? group, string? description, string root) {
        Name = name;
        Identifier = identifier;
        Version = version;
        Group = group;
        Description = description;
        Root = root;
    }
}

/// <summary>
/// Represents a snapshot of an executable object's information for logging.
/// </summary>
public sealed class ExecutableObjectInfo {
    public string ID { get; init; }
    public string Type { get; init; }
    public bool Blocking { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExecutableObjectInfo"/> class from an executable object.
    /// </summary>
    public ExecutableObjectInfo(ExecutableObject obj) {
        ID = obj.GetID();
        Type = obj.GetType().FullName ?? "Unknown";
        Blocking = obj.IsBlocking();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExecutableObjectInfo"/> class with specified properties.
    /// </summary>
    [JsonConstructor]
    public ExecutableObjectInfo(string id, string type, bool blocking) {
        ID = id;
        Type = type;
        Blocking = blocking;
    }
}

/// <summary>
/// Represents a snapshot of an execution layer for logging.
/// </summary>
public sealed class ExecutionLayerInfo {
    public ExecutableObjectInfo[] Items { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExecutionLayerInfo"/> class from an execution layer.
    /// </summary>
    public ExecutionLayerInfo(ExecutionGraph.ExecutionLayer layer) {
        Items = layer.Items.Select(obj => new ExecutableObjectInfo(obj)).ToArray();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExecutionLayerInfo"/> class with a pre-defined set of items.
    /// </summary>
    [JsonConstructor]
    public ExecutionLayerInfo(ExecutableObjectInfo[] items) {
        Items = items;
    }
}

#endregion

#region General Log Entries

/// <summary>
/// A basic log entry associated with a specific plugin.
/// </summary>
public class BasicPluginLogEntry : BaseLogEntry {
    public override LogLevel Level { get; }
    public string Message { get; }
    public PluginInfo Plugin { get; }

    public BasicPluginLogEntry(ManilaPlugin plugin, string message, LogLevel level) {
        Level = level;
        Message = message;
        Plugin = new PluginInfo(plugin);
    }

    [JsonConstructor]
    public BasicPluginLogEntry(PluginInfo plugin, string message, LogLevel level) {
        Level = level;
        Message = message;
        Plugin = plugin;
    }
}

#endregion

#region Build Lifecycle Log Entries

/// <summary>
/// Logged when the engine starts.
/// </summary>
public class EngineStartedLogEntry : BaseLogEntry {
    public override LogLevel Level => LogLevel.System;
    public string RootDirectory { get; }
    public string DataDirectory { get; }

    [JsonConstructor]
    public EngineStartedLogEntry(string rootDir, string dataDir) {
        RootDirectory = rootDir;
        DataDirectory = dataDir;
    }
}

/// <summary>
/// Logged when the build execution graph layers are determined.
/// </summary>
public class BuildLayersLogEntry : BaseLogEntry {
    public override LogLevel Level => LogLevel.System;
    public ExecutionLayerInfo[] Layers { get; }

    public BuildLayersLogEntry(ExecutionGraph.ExecutionLayer[] layers) {
        Layers = layers.Select(layer => new ExecutionLayerInfo(layer)).ToArray();
    }

    [JsonConstructor]
    public BuildLayersLogEntry(ExecutionLayerInfo[] layers) {
        Layers = layers;
    }
}

/// <summary>
/// Logged when the execution of a build layer starts.
/// </summary>
public class BuildLayerStartedLogEntry : BaseLogEntry {
    public override LogLevel Level => LogLevel.Info;
    public ExecutionLayerInfo Layer { get; }
    public int LayerIndex { get; }
    public string ContextID { get; }

    public BuildLayerStartedLogEntry(ExecutionGraph.ExecutionLayer layer, Guid contextID, int layerIndex) {
        Layer = new ExecutionLayerInfo(layer);
        LayerIndex = layerIndex;
        ContextID = contextID.ToString();
    }

    [JsonConstructor]
    public BuildLayerStartedLogEntry(ExecutionLayerInfo layer, Guid contextID, int layerIndex) {
        Layer = layer;
        LayerIndex = layerIndex;
        ContextID = contextID.ToString();
    }
}

/// <summary>
/// Logged when the execution of a build layer completes.
/// </summary>
public class BuildLayerCompletedLogEntry : BaseLogEntry {
    public override LogLevel Level => LogLevel.Info;
    public ExecutionLayerInfo Layer { get; }
    public int LayerIndex { get; }
    public string ContextID { get; }

    public BuildLayerCompletedLogEntry(ExecutionGraph.ExecutionLayer layer, Guid contextID, int layerIndex) {
        Layer = new ExecutionLayerInfo(layer);
        LayerIndex = layerIndex;
        ContextID = contextID.ToString();
    }

    [JsonConstructor]
    public BuildLayerCompletedLogEntry(ExecutionLayerInfo layer, Guid contextID, int layerIndex) {
        Layer = layer;
        LayerIndex = layerIndex;
        ContextID = contextID.ToString();
    }
}

/// <summary>
/// Logged when the overall build process starts.
/// </summary>
public class BuildStartedLogEntry : BaseLogEntry {
    public override LogLevel Level => LogLevel.Info;

    [JsonConstructor]
    public BuildStartedLogEntry() { }
}

/// <summary>
/// Logged when the build completes successfully.
/// </summary>
public class BuildCompletedLogEntry : BaseLogEntry {
    public override LogLevel Level => LogLevel.Info;
    public long Duration { get; }

    [JsonConstructor]
    public BuildCompletedLogEntry(long duration) {
        Duration = duration;
    }
}

/// <summary>
/// Logged when the build fails.
/// </summary>
public class BuildFailedLogEntry : BaseLogEntry {
    public override LogLevel Level => LogLevel.Error;
    public long Duration { get; }
    public Exception Exception { get; }

    [JsonConstructor]
    public BuildFailedLogEntry(long duration, Exception e) {
        Duration = duration;
        Exception = e;
    }
}

#endregion

#region Project & Job Discovery Log Entries

/// <summary>
/// Logged when projects have been fully initialized.
/// </summary>
public class ProjectsInitializedLogEntry : BaseLogEntry {
    public override LogLevel Level => LogLevel.Info;
    public long Duration { get; }

    [JsonConstructor]
    public ProjectsInitializedLogEntry(long duration) {
        Duration = duration;
    }
}

/// <summary>
/// Logged when a project is discovered from a script file.
/// </summary>
public class ProjectDiscoveredLogEntry : BaseLogEntry {
    public override LogLevel Level => LogLevel.System;
    public string Root { get; }
    public string Script { get; }

    [JsonConstructor]
    public ProjectDiscoveredLogEntry(string root, string script) {
        Root = root;
        Script = script;
    }
}

/// <summary>
/// Logged when a discovered project is initialized.
/// </summary>
public class ProjectInitializedLogEntry : BaseLogEntry {
    public override LogLevel Level => LogLevel.System;
    public ProjectInfo Project { get; }

    public ProjectInitializedLogEntry(Project project) {
        Project = new ProjectInfo(project);
    }

    [JsonConstructor]
    public ProjectInitializedLogEntry(ProjectInfo project) {
        Project = project;
    }
}

/// <summary>
/// Logged when a job is discovered within a component.
/// </summary>
public class JobDiscoveredLogEntry : BaseLogEntry {
    public override LogLevel Level => LogLevel.System;
    public ComponentInfo Component { get; }
    public JobInfo Job { get; }

    public JobDiscoveredLogEntry(Job job, Component component) {
        Component = new ComponentInfo(component);
        Job = new JobInfo(job);
    }

    [JsonConstructor]
    public JobDiscoveredLogEntry(JobInfo job, ComponentInfo component) {
        Job = job;
        Component = component;
    }
}

#endregion

#region Script & Job Execution Log Entries

/// <summary>
/// Logged when a script begins execution.
/// </summary>
public class ScriptExecutionStartedLogEntry : BaseLogEntry {
    public override LogLevel Level => LogLevel.Info;
    public string ScriptPath { get; }
    public string ContextID { get; }

    [JsonConstructor]
    public ScriptExecutionStartedLogEntry(string scriptPath, Guid contextID) {
        ScriptPath = scriptPath;
        ContextID = contextID.ToString();
    }
}

/// <summary>
/// Represents a log message originating from within a script.
/// </summary>
public class ScriptLogEntry : BaseLogEntry {
    public override LogLevel Level => LogLevel.Info;
    public string ScriptPath { get; }
    public string Message { get; }
    public string ContextID { get; }

    [JsonConstructor]
    public ScriptLogEntry(string scriptPath, string message, Guid contextID) {
        ScriptPath = scriptPath;
        Message = message;
        ContextID = contextID.ToString();
    }
}

/// <summary>
/// Logged when a script completes successfully.
/// </summary>
public class ScriptExecutedSuccessfullyLogEntry : BaseLogEntry {
    public override LogLevel Level => LogLevel.Info;
    public string ScriptPath { get; }
    public long ExecutionTimeMS { get; }
    public string ContextID { get; }

    [JsonConstructor]
    public ScriptExecutedSuccessfullyLogEntry(string scriptPath, long executionTimeMS, Guid contextID) {
        ScriptPath = scriptPath;
        ExecutionTimeMS = executionTimeMS;
        ContextID = contextID.ToString();
    }
}

/// <summary>
/// Logged when script execution fails.
/// </summary>
public class ScriptExecutionFailedLogEntry : BaseLogEntry {
    public override LogLevel Level => LogLevel.Error;
    public string ScriptPath { get; }
    public Exception Exception { get; }
    public string ContextID { get; }

    [JsonConstructor]
    public ScriptExecutionFailedLogEntry(string scriptPath, Exception exception, Guid contextID) {
        ScriptPath = scriptPath;
        Exception = exception;
        ContextID = contextID.ToString();
    }
}

/// <summary>
/// Logged when a job begins execution.
/// </summary>
public class JobExecutionStartedLogEntry : BaseLogEntry {
    public override LogLevel Level => LogLevel.Info;
    public JobInfo Job { get; }
    public string ContextID { get; }

    public JobExecutionStartedLogEntry(Job job, Guid contextID) {
        Job = new JobInfo(job);
        ContextID = contextID.ToString();
    }

    [JsonConstructor]
    public JobExecutionStartedLogEntry(JobInfo job, Guid contextID) {
        Job = job;
        ContextID = contextID.ToString();
    }
}

/// <summary>
/// Logged when a job finishes execution.
/// </summary>
public class JobExecutionFinishedLogEntry : BaseLogEntry {
    public override LogLevel Level => LogLevel.Info;
    public JobInfo Job { get; }
    public string ContextID { get; }

    public JobExecutionFinishedLogEntry(Job job, Guid contextID) {
        Job = new JobInfo(job);
        ContextID = contextID.ToString();
    }

    [JsonConstructor]
    public JobExecutionFinishedLogEntry(JobInfo job, Guid contextID) {
        Job = job;
        ContextID = contextID.ToString();
    }
}

/// <summary>
/// Logged when a job fails to execute.
/// </summary>
public class JobExecutionFailedLogEntry : BaseLogEntry {
    public override LogLevel Level => LogLevel.Error;
    public JobInfo Job { get; }
    public string ContextID { get; }
    public Exception Exception { get; }

    public JobExecutionFailedLogEntry(Job job, Guid contextID, Exception exception) {
        Job = new JobInfo(job);
        ContextID = contextID.ToString();
        Exception = exception;
    }

    [JsonConstructor]
    public JobExecutionFailedLogEntry(JobInfo job, Guid contextID, Exception exception) {
        Job = job;
        ContextID = contextID.ToString();
        Exception = exception;
    }
}

#endregion

#region Command Execution Log Entries

/// <summary>
/// Logged when an external command is about to be executed.
/// </summary>
public class CommandExecutionLogEntry : BaseLogEntry {
    public override LogLevel Level => LogLevel.Debug;
    public string ContextID { get; }
    public string Executable { get; }
    public string[] Args { get; }
    public string WorkingDir { get; }

    [JsonConstructor]
    public CommandExecutionLogEntry(Guid contextID, string executable, string[] args, string workingDir) {
        ContextID = contextID.ToString();
        Executable = executable;
        Args = args;
        WorkingDir = workingDir;
    }
}

/// <summary>
/// Logged when an external command finishes successfully.
/// </summary>
public class CommandExecutionFinishedLogEntry : BaseLogEntry {
    public override LogLevel Level => LogLevel.Debug;
    public string ContextID { get; }
    public string StdOut { get; }
    public string StdErr { get; }
    public long Duration { get; }
    public int ExitCode { get; }

    [JsonConstructor]
    public CommandExecutionFinishedLogEntry(Guid contextID, string stdOut, string stdErr, long duration, int exitCode) {
        ContextID = contextID.ToString();
        StdOut = stdOut;
        StdErr = stdErr;
        Duration = duration;
        ExitCode = exitCode;
    }
}

/// <summary>
/// Logged when an external command fails.
/// </summary>
public class CommandExecutionFailedLogEntry : BaseLogEntry {
    public override LogLevel Level => LogLevel.Error;
    public string ContextID { get; }
    public string StdOut { get; }
    public string StdErr { get; }
    public long Duration { get; }
    public int ExitCode { get; }

    [JsonConstructor]
    public CommandExecutionFailedLogEntry(Guid contextID, string stdOut, string stdErr, long duration, int exitCode) {
        ContextID = contextID.ToString();
        StdOut = stdOut;
        StdErr = stdErr;
        Duration = duration;
        ExitCode = exitCode;
    }
}

/// <summary>
/// Represents a standard output message from an executed command.
/// </summary>
public class CommandStdOutLogEntry : BaseLogEntry {
    public override LogLevel Level => LogLevel.Info;
    public string ContextID { get; }
    public string Message { get; }
    public bool Quiet { get; }

    [JsonConstructor]
    public CommandStdOutLogEntry(Guid contextID, string message, bool quiet) {
        ContextID = contextID.ToString();
        Message = message;
        Quiet = quiet;
    }
}

/// <summary>
/// Represents a standard error message from an executed command.
/// </summary>
public class CommandStdErrLogEntry : BaseLogEntry {
    public override LogLevel Level => LogLevel.Error;
    public string ContextID { get; }
    public string Message { get; }
    public bool Quiet { get; }

    [JsonConstructor]
    public CommandStdErrLogEntry(Guid contextID, string message, bool quiet) {
        ContextID = contextID.ToString();
        Message = message;
        Quiet = quiet;
    }
}

#endregion

#region Plugin Loading Log Entries

/// <summary>
/// Logged when loading plugins from a specific path.
/// </summary>
public class LoadingPluginsLogEntry : BaseLogEntry {
    public override LogLevel Level => LogLevel.System;
    public string PluginPath { get; }
    public string ContextID { get; }

    [JsonConstructor]
    public LoadingPluginsLogEntry(string pluginPath, Guid contextID) {
        PluginPath = pluginPath;
        ContextID = contextID.ToString();
    }
}

/// <summary>
/// Logged when a specific plugin assembly begins to load.
/// </summary>
public class LoadingPluginLogEntry : BaseLogEntry {
    public override LogLevel Level => LogLevel.Debug;
    public PluginInfo Plugin { get; }
    public string ContextID { get; }

    public LoadingPluginLogEntry(ManilaPlugin plugin, Guid contextID) {
        Plugin = new PluginInfo(plugin);
        ContextID = contextID.ToString();
    }

    [JsonConstructor]
    public LoadingPluginLogEntry(PluginInfo plugin, Guid contextID) {
        Plugin = plugin;
        ContextID = contextID.ToString();
    }
}

/// <summary>
/// Logged when a NuGet package dependency for a plugin is being loaded.
/// </summary>
public class NuGetPackageLoadingLogEntry : BaseLogEntry {
    public override LogLevel Level => LogLevel.Debug;
    public string PackageID { get; }
    public string PackageVersion { get; }
    public PluginInfo Plugin { get; }
    public string ContextID { get; }

    public NuGetPackageLoadingLogEntry(string id, string version, ManilaPlugin plugin, Guid contextID) {
        PackageID = id;
        PackageVersion = version;
        Plugin = new PluginInfo(plugin);
        ContextID = contextID.ToString();
    }

    [JsonConstructor]
    public NuGetPackageLoadingLogEntry(string id, string version, PluginInfo plugin, Guid contextID) {
        PackageID = id;
        PackageVersion = version;
        Plugin = plugin;
        ContextID = contextID.ToString();
    }
}

/// <summary>
/// Logged when a sub-package or assembly from a NuGet dependency is being loaded.
/// Uses regex to parse package details from the assembly path.
/// </summary>
public partial class NuGetSubPackageLoadingEntry : BaseLogEntry {
    public override LogLevel Level => LogLevel.Debug;
    public string PackageID { get; }
    public string PackageVersion { get; }
    public string ContextID { get; }

    [JsonConstructor]
    public NuGetSubPackageLoadingEntry(string assembly, Guid contextID) {
        PackageID = GetPackageID(assembly);
        PackageVersion = GetPackageVersion(assembly);
        ContextID = contextID.ToString();
    }

    private static string GetPackageID(string assembly) {
        var match = AssemblyRegex().Match(assembly);
        return match.Success ? match.Groups["package"].Value : assembly;
    }

    private static string GetPackageVersion(string assembly) {
        var match = AssemblyRegex().Match(assembly);
        return match.Success ? match.Groups["version"].Value : assembly;
    }

    [GeneratedRegex(@"(?<package>[\w\.]+?)_(?<version>[\d\.]+?)[\\\/]")]
    private static partial Regex AssemblyRegex();
}

#endregion

#region  Miscellaneous Log Entries

/// <summary>
/// Represents a log entry for a replayed log entry.
/// This is used to replay cached log entries with a specific context ID.
/// </summary>
/// <param name="entry">The original log entry to replay.</param>
/// <param name="contextID">The context ID to associate with the replayed log entry.</param>
/// <remarks>
/// The <see cref="ParentContextID"/> is always null for replay entries, use <see cref="ContextID"/> instead.
/// </remarks>
public class ReplayLogEntry(ILogEntry entry, Guid contextID) : ILogEntry {
    public ILogEntry Entry { get; } = entry;
    public Guid ContextID { get; } = contextID;

    /// <inheritdoc />
    public long Timestamp { get; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// <inheritdoc />
    public LogLevel Level => Entry.Level;

    /// <summary>
    /// Parent context ID for this log entry is always the same, use <see cref="ContextID"/> instead.
    /// </summary>
    public Guid? ParentContextID { get => Guid.AllBitsSet; set => throw new ManilaException("ParentContextID is not applicable for replayed log entries."); }
}

public class StageChangeLogEntry(ExecutionStages changedFrom, ExecutionStages changedTo, long previousStartedAt) : BaseLogEntry {
    public override LogLevel Level => LogLevel.Debug;
    public ExecutionStages ChangedFrom { get; } = changedFrom;
    public ExecutionStages ChangedTo { get; } = changedTo;
    public long PreviousStartedAt { get; } = previousStartedAt;
}

#endregion
