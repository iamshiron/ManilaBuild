using System.Text.RegularExpressions;
using Shiron.Manila.API;
using Shiron.Manila.Ext;
using Shiron.Manila.Utils;

namespace Shiron.Manila.Logging;

#region Log Data Transfer Objects

/// <summary>
/// Represents a snapshot of a plugin's information for logging.
/// </summary>
public sealed class PluginInfo(ManilaPlugin plugin) {
    public string Name { get; init; } = plugin.Name;
    public string Group { get; init; } = plugin.Group;
    public string Version { get; init; } = plugin.Version;
    public string[] Authors { get; init; } = plugin.Authors.ToArray();
    public string Entry { get; init; } = plugin.GetType().FullName!;
    public string[] NuGetDependencies { get; init; } = plugin.NugetDependencies.ToArray();
    public string File { get; init; } = plugin.File!;
}

/// <summary>
/// Represents a snapshot of a job's information for logging.
/// </summary>
public sealed class JobInfo(API.Job job) {
    public string Name { get; init; } = job.Name;
    public string ID { get; init; } = job.GetIdentifier();
    public string ScriptPath { get; init; } = job.Context.ScriptPath;
    public string Description { get; init; } = job.Description;
    public ComponentInfo Component { get; init; } = new(job.Component);
}

/// <summary>
/// Represents a snapshot of a component's information for logging.
/// </summary>
public sealed class ComponentInfo(Component component) {
    public bool IsProject { get; init; } = component is Project;
    public bool IsWorkspace { get; init; } = component is Workspace;
    public string Root { get; init; } = component.Path.get();
    public string ID { get; init; } = component.GetIdentifier();
}

/// <summary>
/// Represents a snapshot of a project's information for logging.
/// </summary>
public sealed class ProjectInfo(Project project) {
    public string Name { get; init; } = project.Name;
    public string Identifier { get; init; } = project.GetIdentifier();
    public string? Version { get; init; } = project.Version;
    public string? Group { get; init; } = project.Group;
    public string? Description { get; init; } = project.Description;
    public string Root { get; init; } = project.Path.get();
}

/// <summary>
/// Represents a snapshot of an executable object's information for logging.
/// </summary>
public sealed class ExecutableObjectInfo(ExecutableObject obj) {
    public string ID { get; init; } = obj.GetID();
    public string Type { get; init; } = obj.GetType().FullName ?? "Unknown";
    public bool Blocking { get; init; } = obj.IsBlocking();
}

/// <summary>
/// Represents a snapshot of an execution layer for logging.
/// </summary>
public sealed class ExecutionLayerInfo(ExecutionGraph.ExecutionLayer layer) {
    public ExecutableObjectInfo[] Items { get; init; } = layer.Items.Select(obj => new ExecutableObjectInfo(obj)).ToArray();
}

#endregion

#region General Log Entries

/// <summary>
/// A basic log entry associated with a specific plugin.
/// </summary>
public class BasicPluginLogEntry(ManilaPlugin plugin, string message, LogLevel level) : BaseLogEntry {
    public override LogLevel Level { get; } = level;
    public string Message { get; } = message;
    public PluginInfo Plugin { get; } = new(plugin);
}

#endregion

#region Build Lifecycle Log Entries

/// <summary>
/// Logged when the engine starts.
/// </summary>
public class EngineStartedLogEntry(string rootDir, string dataDir) : BaseLogEntry {
    public override LogLevel Level => LogLevel.System;
    public string RootDirectory { get; } = rootDir;
    public string DataDirectory { get; } = dataDir;
}

/// <summary>
/// Logged when the build execution graph layers are determined.
/// </summary>
public class BuildLayersLogEntry(ExecutionGraph.ExecutionLayer[] layers) : BaseLogEntry {
    public override LogLevel Level => LogLevel.System;
    public ExecutionLayerInfo[] Layers { get; } = layers.Select(layer => new ExecutionLayerInfo(layer)).ToArray();
}

/// <summary>
/// Logged when the execution of a build layer starts.
/// </summary>
public class BuildLayerStartedLogEntry(ExecutionGraph.ExecutionLayer layer, Guid contextID, int layerIndex) : BaseLogEntry {
    public override LogLevel Level => LogLevel.Info;
    public ExecutionLayerInfo Layer { get; } = new(layer);
    public int LayerIndex { get; } = layerIndex;
    public string ContextID { get; } = contextID.ToString();
}

/// <summary>
/// Logged when the execution of a build layer completes.
/// </summary>
public class BuildLayerCompletedLogEntry(ExecutionGraph.ExecutionLayer layer, Guid contextID, int layerIndex) : BaseLogEntry {
    public override LogLevel Level => LogLevel.Info;
    public ExecutionLayerInfo Layer { get; } = new(layer);
    public int LayerIndex { get; } = layerIndex;
    public string ContextID { get; } = contextID.ToString();
}

/// <summary>
/// Logged when the overall build process starts.
/// </summary>
public class BuildStartedLogEntry : BaseLogEntry {
    public override LogLevel Level => LogLevel.Info;
}

/// <summary>
/// Logged when the build completes successfully.
/// </summary>
public class BuildCompletedLogEntry(long duration) : BaseLogEntry {
    public override LogLevel Level => LogLevel.Info;
    public long Duration { get; } = duration;
}

/// <summary>
/// Logged when the build fails.
/// </summary>
public class BuildFailedLogEntry(long duration, Exception e) : BaseLogEntry {
    public override LogLevel Level => LogLevel.Error;
    public long Duration { get; } = duration;
    public Exception Exception { get; } = e;
}

#endregion

#region Project & Job Discovery Log Entries

/// <summary>
/// Logged when projects have been fully initialized.
/// </summary>
public class ProjectsInitializedLogEntry(long duration) : BaseLogEntry {
    public override LogLevel Level => LogLevel.Info;
    public long Duration { get; } = duration;
}

/// <summary>
/// Logged when a project is discovered from a script file.
/// </summary>
public class ProjectDiscoveredLogEntry(string root, string script) : BaseLogEntry {
    public override LogLevel Level => LogLevel.System;
    public string Root { get; } = root;
    public string Script { get; } = script;
}

/// <summary>
/// Logged when a discovered project is initialized.
/// </summary>
public class ProjectInitializedLogEntry(Project project) : BaseLogEntry {
    public override LogLevel Level => LogLevel.System;
    public ProjectInfo Project { get; } = new(project);
}

/// <summary>
/// Logged when a job is discovered within a component.
/// </summary>
public class JobDiscoveredLogEntry(API.Job job, Component component) : BaseLogEntry {
    public override LogLevel Level => LogLevel.System;
    public ComponentInfo Component { get; } = new(component);
    public JobInfo Job { get; } = new(job);
}

#endregion

#region Script & Job Execution Log Entries

/// <summary>
/// Logged when a script begins execution.
/// </summary>
public class ScriptExecutionStartedLogEntry(string scriptPath, Guid contextID) : BaseLogEntry {
    public override LogLevel Level => LogLevel.Info;
    public string ScriptPath { get; } = scriptPath;
    public string ContextID { get; } = contextID.ToString();
}

/// <summary>
/// Represents a log message originating from within a script.
/// </summary>
public class ScriptLogEntry(string scriptPath, string message, Guid contextID) : BaseLogEntry {
    public override LogLevel Level => LogLevel.Info;
    public string ScriptPath { get; } = scriptPath;
    public string Message { get; } = message;
    public string ContextID { get; } = contextID.ToString();
}

/// <summary>
/// Logged when a script completes successfully.
/// </summary>
public class ScriptExecutedSuccessfullyLogEntry(string scriptPath, long executionTimeMS, Guid contextID) : BaseLogEntry {
    public override LogLevel Level => LogLevel.Info;
    public string ScriptPath { get; } = scriptPath;
    public long ExecutionTimeMS { get; } = executionTimeMS;
    public string ContextID { get; } = contextID.ToString();
}

/// <summary>
/// Logged when script execution fails.
/// </summary>
public class ScriptExecutionFailedLogEntry(string scriptPath, Exception exception, Guid contextID) : BaseLogEntry {
    public override LogLevel Level => LogLevel.Error;
    public string ScriptPath { get; } = scriptPath;
    public Exception Exception { get; } = exception;
    public string ContextID { get; } = contextID.ToString();
}

/// <summary>
/// Logged when a job begins execution.
/// </summary>
public class JobExecutionStartedLogEntry(API.Job job, Guid contextID) : BaseLogEntry {
    public override LogLevel Level => LogLevel.Info;
    public JobInfo Job { get; } = new(job);
    public string ContextID { get; } = contextID.ToString();
}

/// <summary>
/// Logged when a job finishes execution.
/// </summary>
public class JobExecutionFinishedLogEntry(API.Job job, Guid contextID) : BaseLogEntry {
    public override LogLevel Level => LogLevel.Info;
    public JobInfo Job { get; } = new(job);
    public string ContextID { get; } = contextID.ToString();
}

/// <summary>
/// Logged when a job fails to execute.
/// </summary>
public class JobExecutionFailedLogEntry(API.Job job, Guid contextID, Exception exception) : BaseLogEntry {
    public override LogLevel Level => LogLevel.Error;
    public JobInfo Job { get; } = new(job);
    public string ContextID { get; } = contextID.ToString();
    public Exception Exception { get; } = exception;
}

#endregion

#region Command Execution Log Entries

/// <summary>
/// Logged when an external command is about to be executed.
/// </summary>
public class CommandExecutionLogEntry(Guid contextID, string executable, string[] args, string workingDir) : BaseLogEntry {
    public override LogLevel Level => LogLevel.Debug;
    public string ContextID { get; } = contextID.ToString();
    public string Executable { get; } = executable;
    public string[] Args { get; } = args;
    public string WorkingDir { get; } = workingDir;
}

/// <summary>
/// Logged when an external command finishes successfully.
/// </summary>
public class CommandExecutionFinishedLogEntry(Guid contextID, string stdOut, string stdErr, long duration, int exitCode) : BaseLogEntry {
    public override LogLevel Level => LogLevel.Debug;
    public string ContextID { get; } = contextID.ToString();
    public string StdOut { get; } = stdOut;
    public string StdErr { get; } = stdErr;
    public long Duration { get; } = duration;
    public int ExitCode { get; } = exitCode;
}

/// <summary>
/// Logged when an external command fails.
/// </summary>
public class CommandExecutionFailedLogEntry(Guid contextID, string stdOut, string stdErr, long duration, int exitCode) : BaseLogEntry {
    public override LogLevel Level => LogLevel.Error;
    public string ContextID { get; } = contextID.ToString();
    public string StdOut { get; } = stdOut;
    public string StdErr { get; } = stdErr;
    public long Duration { get; } = duration;
    public int ExitCode { get; } = exitCode;
}

/// <summary>
/// Represents a standard output message from an executed command.
/// </summary>
public class CommandStdOutLogEntry(Guid contextID, string message, bool quiet) : BaseLogEntry {
    public override LogLevel Level => LogLevel.Info;
    public string ContextID { get; } = contextID.ToString();
    public string Message { get; } = message;
    public bool Quiet { get; } = quiet;
}

/// <summary>
/// Represents a standard error message from an executed command.
/// </summary>
public class CommandStdErrLogEntry(Guid contextID, string message, bool quiet) : BaseLogEntry {
    public override LogLevel Level => LogLevel.Error;
    public string ContextID { get; } = contextID.ToString();
    public string Message { get; } = message;
    public bool Quiet { get; } = quiet;
}

#endregion

#region Plugin Loading Log Entries

/// <summary>
/// Logged when loading plugins from a specific path.
/// </summary>
public class LoadingPluginsLogEntry(string pluginPath, Guid contextID) : BaseLogEntry {
    public override LogLevel Level => LogLevel.System;
    public string PluginPath { get; } = pluginPath;
    public string ContextID { get; } = contextID.ToString();
}

/// <summary>
/// Logged when a specific plugin assembly begins to load.
/// </summary>
public class LoadingPluginLogEntry(ManilaPlugin plugin, Guid contextID) : BaseLogEntry {
    public override LogLevel Level => LogLevel.Debug;
    public PluginInfo Plugin { get; } = new(plugin);
    public string ContextID { get; } = contextID.ToString();
}

/// <summary>
/// Logged when a NuGet package dependency for a plugin is being loaded.
/// </summary>
public class NuGetPackageLoadingLogEntry(string id, string version, ManilaPlugin plugin, Guid contextID) : BaseLogEntry {
    public override LogLevel Level => LogLevel.Debug;
    public string PackageID { get; } = id;
    public string PackageVersion { get; } = version;
    public PluginInfo Plugin { get; } = new(plugin);
    public string ContextID { get; } = contextID.ToString();
}

/// <summary>
/// Logged when a sub-package or assembly from a NuGet dependency is being loaded.
/// Uses regex to parse package details from the assembly path.
/// </summary>
public partial class NuGetSubPackageLoadingEntry(string assembly, Guid contextID) : BaseLogEntry {
    public override LogLevel Level => LogLevel.Debug;
    public string PackageID { get; } = GetPackageID(assembly);
    public string PackageVersion { get; } = GetPackageVersion(assembly);
    public string ContextID { get; } = contextID.ToString();

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

