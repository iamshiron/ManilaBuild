
using Newtonsoft.Json;
using Shiron.Manila.Logging;

namespace Shiron.Manila.Utils.Logging;

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
