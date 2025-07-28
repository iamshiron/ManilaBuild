using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Shiron.Manila.API;
using Shiron.Manila.API.Ext;
using Shiron.Manila.API.Interfaces;
using Shiron.Manila.API.Logging;
using Shiron.Manila.Enums;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Ext;
using Shiron.Manila.Services;
using Shiron.Manila.Utils;

namespace Shiron.Manila.Logging;

#region Log Data Transfer Objects

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
    public override LogLevel Level => LogLevel.System;
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

public class StageChangeLogEntry(ExecutionStages changedFrom, ExecutionStages changedTo, long previousStartedAt) : BaseLogEntry {
    public override LogLevel Level => LogLevel.Debug;
    public ExecutionStages ChangedFrom { get; } = changedFrom;
    public ExecutionStages ChangedTo { get; } = changedTo;
    public long PreviousStartedAt { get; } = previousStartedAt;
}

#endregion
