using Newtonsoft.Json;

namespace Shiron.Manila.Profiling;

/// <summary>
/// Represents a single event in the Chrome Trace Event Format.
/// </summary>
/// <remarks>
/// This structure is designed to be serialized directly to JSON for chrome://tracing.
/// Property names are intentionally lowercase to match the Chrome Trace Event Format specification.
/// </remarks>
public class TraceEvent {
    /// <summary>
    /// The name of the event.
    /// </summary>
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The category of the event.
    /// </summary>
    [JsonProperty("cat")]
    public string Category { get; set; } = "default";

    /// <summary>
    /// The phase of the event (e.g., "B" for begin, "E" for end, "X" for complete).
    /// </summary>
    [JsonProperty("ph")]
    public string Phase { get; set; } = string.Empty;

    /// <summary>
    /// The timestamp of the event in microseconds.
    /// </summary>
    [JsonProperty("ts")]
    public long Timestamp { get; set; }

    /// <summary>
    /// The process ID.
    /// </summary>
    [JsonProperty("pid")]
    public int ProcessId { get; set; }

    /// <summary>
    /// The thread ID.
    /// </summary>
    [JsonProperty("tid")]
    public int ThreadId { get; set; }

    /// <summary>
    /// Optional arguments associated with the event.
    /// </summary>
    [JsonProperty("args")]
    public Dictionary<string, object> Arguments { get; set; } = [];

    /// <summary>
    /// Duration of the event in microseconds (only for "X" phase events).
    /// </summary>
    [JsonProperty("dur")]
    public long Duration { get; set; }
}
