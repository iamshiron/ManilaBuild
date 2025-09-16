
namespace Shiron.Manila.Exceptions;

/// <summary>
/// Represents the base class for all exceptions thrown by the Manila application.
/// </summary>
public class ManilaException : Exception {
    public ManilaException() { }
    public ManilaException(string message) : base(message) { }
    public ManilaException(string message, Exception? innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when there is an error in the user-defined configuration,
/// such as in a Manila.js script, project structure, or build parameters.
/// This includes validation, resolution, and structural errors.
/// </summary>
public class ConfigurationException : ManilaException {
    public ConfigurationException() { }
    public ConfigurationException(string message) : base(message) { }
    public ConfigurationException(string message, Exception? innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown for errors related to loading, resolving, or managing plugins and their components.
/// </summary>
public class PluginException : ManilaException {
    public PluginException() { }
    public PluginException(string message) : base(message) { }
    public PluginException(string message, Exception? innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when a failure occurs during the artifact generation or build process,
/// after configuration has been successfully parsed.
/// </summary>
public class BuildProcessException : ManilaException {
    public BuildProcessException() { }
    public BuildProcessException(string message) : base(message) { }
    public BuildProcessException(string message, Exception? innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when an error occurs during the runtime execution or compilation of a
/// JavaScript file by the script engine. This wraps exceptions originating from
/// the script itself or from the engine's processing of the script.
/// </summary>
public class ScriptExecutionException : ManilaException {
    /// <summary>
    /// The path to the script that failed.
    /// </summary>
    public string? ScriptPath { get; }

    /// <summary>
    /// The 'name' of the error from JavaScript (e.g., "TypeError", "ReferenceError").
    /// </summary>
    public string? JavaScriptErrorName { get; }

    /// <summary>
    /// The stack trace originating from the JavaScript code.
    /// </summary>
    public string? JavaScriptStackTrace { get; }

    public ScriptExecutionException() { }
    public ScriptExecutionException(string message) : base(message) { }
    public ScriptExecutionException(string message, Exception? innerException) : base(message, innerException) { }

    public ScriptExecutionException(string message, string scriptPath, Exception? innerException = null)
        : base(message, innerException) {
        ScriptPath = scriptPath;
    }

    public ScriptExecutionException(
        string message,
        string scriptPath,
        string? jsErrorName,
        string? jsStackTrace,
        Exception? innerException = null
    ) : base(message, innerException) {
        ScriptPath = scriptPath;
        JavaScriptErrorName = jsErrorName;
        JavaScriptStackTrace = jsStackTrace;
    }
}
