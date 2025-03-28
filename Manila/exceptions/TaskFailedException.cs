namespace Shiron.Manila.Exceptions;

public class TaskFailedException : Exception {
    public readonly API.Task Task;

    public TaskFailedException(API.Task task) : base("Task failed: " + task.name) {
        Task = task;
    }
    public TaskFailedException(API.Task task, Exception innerException) : base("Task failed: " + task.name, innerException) {
        Task = task;
    }
}
