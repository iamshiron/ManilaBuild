using Shiron.Manila.API;

namespace Shiron.Manila.Exceptions;

/// <summary>
/// Thrown when a job fails during its execution. It captures the specific job
/// and the underlying exception for detailed diagnostics.
/// </summary>
public class JobFailedException(Job job, Exception innerException) : BuildTimeException($"Job '{job.GetIdentifier()}' failed during execution.", innerException) {
    /// <summary>
    /// The job that failed, providing context for the error.
    /// </summary>
    public readonly Job Job = job;
}

/// <summary>
/// Thrown when a job with the specified key cannot be found in the workspace or project.
/// </summary>
public class JobNotFoundException(string key) : BuildTimeException($"Job '{key}' not found.") {
    /// <summary>
    /// The key of the job that was not found.
    /// </summary>
    public readonly string JobKey = key;
}
