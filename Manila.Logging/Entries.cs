
namespace Shiron.Manila.Logging;

/// <summary>
/// Defines the essential properties for any log entry.
/// </summary>
public interface ILogEntry {
    /// <summary>
    /// The UTC timestamp when the log entry was created, in Unix milliseconds.
    /// </summary>
    long Timestamp { get; }

    /// <summary>
    /// The severity level of the log entry.
    /// </summary>
    LogLevel Level { get; }

    Guid? ParentContextID { get; set; }
}

/// <summary>
/// A base implementation of <see cref="ILogEntry"/> that provides a timestamp
/// and captures the current logging context ID.
/// </summary>
public abstract class BaseLogEntry : ILogEntry {
    /// <inheritdoc />
    public long Timestamp { get; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// <inheritdoc />
    public abstract LogLevel Level { get; }

    /// <summary>
    /// The ID of the parent logging context, if any.
    /// </summary>
    public virtual Guid? ParentContextID { get; set; }
}

/// <summary>
/// A basic log entry with a simple message.
/// </summary>
public class BasicLogEntry(string message, LogLevel level, string? loggerPrefix = null) : BaseLogEntry {
    public override LogLevel Level { get; } = level;
    public string Message { get; } = message;
    public string? LoggerPrefix { get; } = loggerPrefix;
}
