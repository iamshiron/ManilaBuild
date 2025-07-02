namespace Shiron.Manila.Exceptions;

public class TaskFailedException : Exception {
    public readonly API.Task Task;

    public TaskFailedException(API.Task task) : base("Task failed: " + task.Name) {
        Task = task;
    }
    public TaskFailedException(API.Task task, Exception innerException) : base("Task failed: " + task.Name, innerException) {
        Task = task;
    }
}
