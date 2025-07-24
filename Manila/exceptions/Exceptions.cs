
namespace Shiron.Manila.Exceptions;

#region Base Exceptions

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
/// Thrown when an unexpected internal state or logical contradiction is detected,
/// such as a cycle in a dependency graph. This often indicates a bug in the application itself.
/// </summary>
public class InternalLogicException : ManilaException {
    public InternalLogicException() { }
    public InternalLogicException(string message) : base(message) { }
    public InternalLogicException(string message, Exception? innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when the command-line interface (CLI) fails to initialize properly,
/// often due to missing services or invalid startup parameters.
/// </summary>
public class CliInitializationException : ManilaException {
    public CliInitializationException() { }
    public CliInitializationException(string message) : base(message) { }
    public CliInitializationException(string message, Exception? innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown for errors related to the external environment, such as the operating system,
/// file system, or required framework information.
/// </summary>
public class EnvironmentException : ManilaException {
    public EnvironmentException() { }
    public EnvironmentException(string message) : base(message) { }
    public EnvironmentException(string message, Exception? innerException) : base(message, innerException) { }
}

#endregion

#region Script Exceptions

#endregion

#region Configuration Exceptions

/// <summary>
/// Thrown during dependency graph construction when a job's dependency cannot be found.
/// This is a form of configuration error.
/// </summary>
public class DependencyNotFoundException : ConfigurationException {
    /// <summary>
    /// The name of the job that declared the dependency.
    /// </summary>
    public string JobName { get; }

    /// <summary>
    /// The name of the dependency that could not be found.
    /// </summary>
    public string MissingDependencyName { get; }

    public DependencyNotFoundException(
        string message,
        string jobName,
        string missingDependencyName,
        Exception? innerException = null
    ) : base(message, innerException) {
        JobName = jobName;
        MissingDependencyName = missingDependencyName;
    }
}

#endregion
