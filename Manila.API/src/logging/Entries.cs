
using Newtonsoft.Json;
using Shiron.Manila.API.Ext;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Logging;

namespace Shiron.Manila.API.Logging;

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
