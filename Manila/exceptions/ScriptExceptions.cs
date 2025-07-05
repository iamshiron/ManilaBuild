namespace Shiron.Manila.Exceptions;

/// <summary>
/// Represents an error that originated from the scripting engine or during the
/// execution of a user-provided script file.
/// </summary>
public class ScriptingException : ManilaException {
    public ScriptingException(string message) : base(message) { }

    public ScriptingException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when an operation is performed in the wrong context
/// (e.g., calling a project-specific function at the workspace level).
/// </summary>
public class ContextException : ScriptingException {
    /// <summary>
    /// The context that was actually used.
    /// </summary>
    public readonly Context Is;

    /// <summary>
    /// The context that should have been used.
    /// </summary>
    public readonly Context Should;

    public ContextException(Context isContext, Context shouldContext)
        : base($"Wrong Context! Is: {isContext}, Should be: {shouldContext}") {
        Is = isContext;
        Should = shouldContext;
    }
}

/// <summary>
/// Defines the available execution contexts within Manila.
/// </summary>
public enum Context {
    PROJECT,
    WORKSPACE
}
