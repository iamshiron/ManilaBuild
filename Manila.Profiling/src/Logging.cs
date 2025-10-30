
using System.Text.Json.Serialization;
using Shiron.Manila.Logging;
using Shiron.Manila.Profiling;

namespace Shiron.Manila.Profiling;

public class ProfilingLogEntry : BaseLogEntry {
    public override LogLevel Level => LogLevel.System;
    public string Name { get; }
    public string Category { get; }
    public long ProcessID { get; }
    public long ThreadID { get; }
    public long TraceTimestamp { get; }

    public ProfilingLogEntry(TraceEvent e) {
        Name = e.Name;
        Category = e.Category;
        ProcessID = e.ProcessId;
        ThreadID = e.ThreadId;
        TraceTimestamp = e.Timestamp;
    }

    [JsonConstructor]
    public ProfilingLogEntry(string name, string category, long processID, long threadID, long traceTimestamp) {
        Name = name;
        Category = category;
        ProcessID = processID;
        ThreadID = threadID;
        TraceTimestamp = traceTimestamp;
    }
}

public class ProfileBeginLogEntry : ProfilingLogEntry {
    public ProfileBeginLogEntry(TraceEvent e) : base(e) {
    }

    [JsonConstructor]
    public ProfileBeginLogEntry(string name, string category, long processID, long threadID, long traceTimestamp) : base(name, category, processID, threadID, traceTimestamp) {
    }
}

public class ProfileEndLogEntry : ProfilingLogEntry {
    public ProfileEndLogEntry(TraceEvent e) : base(e) {
    }

    [JsonConstructor]
    public ProfileEndLogEntry(string name, string category, long processID, long threadID, long traceTimestamp) : base(name, category, processID, threadID, traceTimestamp) {
    }
}

public class ProfileCompleteLogEntry : ProfilingLogEntry {
    public long Duration { get; }

    public ProfileCompleteLogEntry(TraceEvent e) : base(e) {
        Duration = e.Duration;
    }

    [JsonConstructor]
    public ProfileCompleteLogEntry(string name, string category, long processID, long threadID, long traceTimestamp, long duration)
        : base(new TraceEvent {
            Name = name,
            Category = category,
            ProcessId = (int) processID,
            ThreadId = (int) threadID,
            Timestamp = traceTimestamp,
            Duration = duration
        }) {
        Duration = duration;
    }
}
