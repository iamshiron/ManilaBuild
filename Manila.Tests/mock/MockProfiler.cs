
using Shiron.Manila.Profiling;

public class MockProfiler : IProfiler {
    public void BeginEvent(string name, Dictionary<string, object>? args = null) { }
    public void EndEvent(string name, Dictionary<string, object>? args = null) { }
    public void RecordCompleteEvent(string name, long timestampMicroseconds, long durationMicroseconds, Dictionary<string, object>? args = null) { }
    public void SaveToFile(string baseDir) { }
}
