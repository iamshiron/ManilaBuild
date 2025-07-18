using Shiron.Manila.API;

namespace Shiron.Manila.Exceptions;

/// <summary>
/// Represents errors that occur specifically during the build process,
/// such as job failures or dependency resolution issues.
/// </summary>
public class BuildException : ManilaException {
    public BuildException(string message) : base(message) { }

    public BuildException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when a job fails during its execution. It captures the specific job
/// and the underlying exception for detailed diagnostics.
/// </summary>
public class JobFailedException : BuildException {
    /// <summary>
    /// The job that failed, providing context for the error.
    /// </summary>
    public readonly Job Job;

    public JobFailedException(Job job, Exception innerException)
        : base($"Job '{job.GetIdentifier()}' failed during execution.", innerException) {
        Job = job;
    }
}

/// <summary>
/// Thrown when a job with the specified key cannot be found in the workspace or project.
/// </summary>
public class JobNotFoundException : BuildException {
    /// <summary>
    /// The key of the job that was not found.
    /// </summary>
    public readonly string JobKey;

    public JobNotFoundException(string key) : base($"Job '{key}' not found.") {
        JobKey = key;
    }
}
