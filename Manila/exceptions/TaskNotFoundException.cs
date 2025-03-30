namespace Shiron.Manila.Exceptions;

public class TaskNotFoundException : Exception {
    public readonly string Key;

    public TaskNotFoundException(string key) : base($"Task '{key}' not found.") {
        Key = key;
    }
    public TaskNotFoundException(string key, Exception innerException) : base($"Task '{key}' not found.", innerException) {
        Key = key;
    }
}
