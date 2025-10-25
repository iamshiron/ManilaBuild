
using Microsoft.CodeAnalysis;

namespace Shiron.Manila.API.Exceptions;

#region Base Exceptions

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

public class ScriptCompilationException : ManilaException {
    public readonly IReadOnlyList<Diagnostic> Diagnostics;

    public ScriptCompilationException(string message, IEnumerable<Diagnostic> diagnostics) : base(message) {
        Diagnostics = [.. diagnostics];
    }
    public ScriptCompilationException(string message, IList<Diagnostic> diagnostics, Exception? innerException) : base(message, innerException) {
        Diagnostics = [.. diagnostics];
    }
}
