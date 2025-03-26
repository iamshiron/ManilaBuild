namespace Shiron.Manila.Exceptions;

/// <summary>
/// Thrown when something is used in the wrong context.
/// </summary>
/// <param name="cIs">The context that was used</param>
/// <param name="cShould">The context the attribute is available</param>
public class ContextException(Context cIs, Context cShould) : Exception("Wrong Context! Is: " + cIs + ", Should: " + cShould) {
    /// <summary>
    /// The context that was used.
    /// </summary>
    public readonly Context cIs = cIs;
    /// <summary>
    /// The context the attribute is available.
    /// </summary>
    public readonly Context cShould = cShould;
}

/// <summary>
/// The available context types.
/// </summary>
public enum Context {
    PROJECT,
    WORKSPACE
}
