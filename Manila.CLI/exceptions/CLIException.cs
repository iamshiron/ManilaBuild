
using Shiron.Manila.Exceptions;

namespace Shiron.Manila.CLI.Exceptions;

public class CLIException : ManilaException {
    public CLIException(string message) : base(message) { }

    public CLIException(string message, Exception innerException) : base(message, innerException) { }
}

public class TaskNotFoundException : BuildException {
    public readonly string TaskKey;

    public TaskNotFoundException(string key) : base($"Task '{key}' not found.") {
        TaskKey = key;
    }
}
