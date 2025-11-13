
namespace Shiron.Manila.Logging;

/// <summary>Log entry contract.</summary>
public interface ILogEntry {
    /// <summary>Unix ms timestamp.</summary>
    long Timestamp { get; }

    /// <summary>Severity level.</summary>
    LogLevel Level { get; }

    Guid? ParentContextID { get; set; }
}

/// <summary>Base log entry.</summary>
public abstract class BaseLogEntry : ILogEntry {
    /// <inheritdoc />
    public long Timestamp { get; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// <inheritdoc />
    public abstract LogLevel Level { get; }

    /// <summary>Parent context ID.</summary>
    public virtual Guid? ParentContextID { get; set; }
}

/// <summary>Message entry.</summary>
public class BasicLogEntry(string message, LogLevel level, string? loggerPrefix = null) : BaseLogEntry {
    public override LogLevel Level { get; } = level;
    public string Message { get; } = message;
    public string? LoggerPrefix { get; } = loggerPrefix;
}

/// <summary>Markup (Info level) entry.</summary>
public class MarkupLogEntry(string message, bool logAlways = false, string? loggerPrefix = null) : BaseLogEntry {
    public override LogLevel Level { get; } = LogLevel.Info;
    public string Message { get; } = message;
    public string? LoggerPrefix { get; } = loggerPrefix;
    public bool LogAlways { get; } = logAlways;
}
