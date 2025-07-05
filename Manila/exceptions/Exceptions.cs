namespace Shiron.Manila.Exceptions;

/// <summary>
/// Base class for all custom exceptions in the Manila application.
/// This allows for a single catch block to handle all application-specific errors,
/// providing a common type to catch for logging or general error handling.
/// </summary>
public class ManilaException : Exception {
    public ManilaException(string message) : base(message) { }

    public ManilaException(string message, Exception innerException) : base(message, innerException) { }
}
