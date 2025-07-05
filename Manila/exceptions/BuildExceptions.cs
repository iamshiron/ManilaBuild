namespace Shiron.Manila.Exceptions;

/// <summary>
/// Represents errors that occur specifically during the build process,
/// such as task failures or dependency resolution issues.
/// </summary>
public class BuildException : ManilaException {
    public BuildException(string message) : base(message) { }

    public BuildException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when a task fails during its execution. It captures the specific task
/// and the underlying exception for detailed diagnostics.
/// </summary>
public class TaskFailedException : BuildException {
    /// <summary>
    /// The task that failed, providing context for the error.
    /// </summary>
    public readonly API.Task Task;

    public TaskFailedException(API.Task task, Exception innerException)
        : base($"Task '{task.GetIdentifier()}' failed during execution.", innerException) {
        Task = task;
    }
}

/// <summary>
/// Thrown when a task with the specified key cannot be found in the workspace or project.
/// </summary>
public class TaskNotFoundException : BuildException {
    /// <summary>
    /// The key of the task that was not found.
    /// </summary>
    public readonly string TaskKey;

    public TaskNotFoundException(string key) : base($"Task '{key}' not found.") {
        TaskKey = key;
    }
}
