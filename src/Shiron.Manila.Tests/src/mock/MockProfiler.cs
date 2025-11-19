
using Shiron.Profiling;
using Shiron.Utils;

public class MockProfiler : IProfiler {
    private readonly long _startTimestamp = TimeUtils.Now();

    public void BeginEvent(string name, Dictionary<string, object>? args = null) { }
    public void EndEvent(string name, Dictionary<string, object>? args = null) { }
    public void RecordCompleteEvent(string name, long timestampMicroseconds, long durationMicroseconds, Dictionary<string, object>? args = null) { }
    public void SaveToFile(string baseDir) { }
    public long GetTimestamp() => TimeUtils.Now() - _startTimestamp;
}
