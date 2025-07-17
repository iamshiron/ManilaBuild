
using Shiron.Manila.Exceptions;

namespace Shiron.Manila.CLI.Exceptions;

public class CLIException : ManilaException {
    public CLIException(string message) : base(message) { }

    public CLIException(string message, Exception innerException) : base(message, innerException) { }
}

public class JobNotFoundException : BuildException {
    public readonly string JobKey;

    public JobNotFoundException(string key) : base($"Job '{key}' not found.") {
        JobKey = key;
    }
}
