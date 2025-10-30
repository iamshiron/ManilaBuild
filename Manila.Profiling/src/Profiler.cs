using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Shiron.Manila.Logging;

namespace Shiron.Manila.Profiling;

public interface IProfiler {
    void BeginEvent(string name, Dictionary<string, object>? args = null);
    void EndEvent(string name, Dictionary<string, object>? args = null);
    void SaveToFile(string baseDir);
    long GetTimestamp();

    void RecordCompleteEvent(string name, long timestampMicroseconds, long durationMicroseconds, Dictionary<string, object>? args = null);
}

/// <summary>
/// Provides a simple API for profiling code execution using the Chrome Trace Event Format.
/// Events are collected and can be saved to a JSON file for visualization in chrome://tracing.
/// </summary>
public class Profiler(ILogger logger, bool logProfiling) : IProfiler {
    private readonly ILogger _logger = logger;
    private readonly bool _logProfiling = logProfiling;
    private readonly List<TraceEvent> _events = [];
    private readonly Stopwatch _stopwatch = new();
    private readonly int _processId = Environment.ProcessId;
    private readonly Lock _lock = new();
    private readonly long _profilerStartTimestampMicroseconds = Utils.TimeNow();

    internal static readonly AsyncLocal<string?> _currentCategory = new();

    public void BeginEvent(string name, Dictionary<string, object>? args = null) {
        var e = new TraceEvent {
            Name = name,
            Category = _currentCategory.Value ?? "default",
            Phase = "B", // Begin event
            Timestamp = GetTimestamp(),
            ProcessId = _processId,
            ThreadId = Environment.CurrentManagedThreadId, // More modern way to get ThreadId
            Arguments = args ?? []
        };

        if (_logProfiling) _logger.Log(new ProfileBeginLogEntry(e));

        lock (_lock) {
            _events.Add(e);
        }
    }

    public void EndEvent(string name, Dictionary<string, object>? args = null) {
        var e = new TraceEvent {
            Name = name,
            Category = _currentCategory.Value ?? "default",
            Phase = "E", // End event
            Timestamp = GetTimestamp(),
            ProcessId = _processId,
            ThreadId = Environment.CurrentManagedThreadId,
            Arguments = args ?? []
        };

        if (_logProfiling) _logger.Log(new ProfileEndLogEntry(e));

        lock (_lock) {
            _events.Add(e);
        }
    }

    public void RecordCompleteEvent(string name, long timestampMicroseconds, long durationMicroseconds, Dictionary<string, object>? args = null) {
        var e = new TraceEvent {
            Name = name,
            Category = _currentCategory.Value ?? "default",
            Phase = "X", // Complete event
            Timestamp = timestampMicroseconds,
            Duration = durationMicroseconds,
            ProcessId = _processId,
            ThreadId = Environment.CurrentManagedThreadId,
            Arguments = args ?? []
        };

        if (_logProfiling) _logger.Log(new ProfileCompleteLogEntry(e));

        lock (_lock) {
            _events.Add(e);
        }
    }

    public long GetTimestamp() {
        // return _stopwatch.ElapsedTicks * 1_000_000 / Stopwatch.Frequency;
        return Utils.TimeNow() - _profilerStartTimestampMicroseconds;
    }

    public void ClearEvents() {
        lock (_lock) {
            _events.Clear();
            _stopwatch.Reset(); // Reset the stopwatch as well
        }
    }

    public void SaveToFile(string baseDir) {
        if (!Directory.Exists(baseDir)) {
            _logger.Debug($"Base directory does not exist: {baseDir}. Profile data will not be saved.");
            return;
        }

        lock (_lock) {
            string timestamp = DateTime.Now.ToString("yyyy-MM_dd-HH_mm_ss");
            string fileName = $"profile-{timestamp}.json";
            string filePath = Path.Combine(baseDir, fileName);

            var settings = new JsonSerializerSettings { Formatting = Formatting.None }; // Use no formatting for smaller file sizes
            string jsonString = JsonConvert.SerializeObject(_events, settings);
            File.WriteAllText(filePath, jsonString);
            _logger.Debug($"Profiling data saved to: {filePath}");
        }
    }
}

public readonly struct ProfileCategory : IDisposable {
    private readonly string? _previousCategory;

    public ProfileCategory(string categoryName) {
        _previousCategory = Profiler._currentCategory.Value;
        Profiler._currentCategory.Value = categoryName;
    }

    public void Dispose() {
        Profiler._currentCategory.Value = _previousCategory;
    }
}

public readonly struct ProfileScope : IDisposable {
    private readonly IProfiler _profiler;
    private readonly string _name;
    private readonly long _startTimestampMicroseconds;
    private readonly Dictionary<string, object>? _args;

    public ProfileScope(IProfiler profiler, Dictionary<string, object>? args = null, [CallerMemberName] string name = "") {
        _profiler = profiler;
        _args = args;
        _name = name;
        _startTimestampMicroseconds = _profiler.GetTimestamp();

        _profiler.BeginEvent(_name, _args);
    }

    public ProfileScope(IProfiler profiler, string name, Dictionary<string, object>? args = null) {
        _profiler = profiler;
        _name = name;
        _args = args;
        _startTimestampMicroseconds = _profiler.GetTimestamp();

        _profiler.BeginEvent(_name, _args);
    }

    public ProfileScope(IProfiler profiler, MethodBase func, Dictionary<string, object>? args = null) {
        _profiler = profiler;
        _name = $"{func.DeclaringType?.FullName ?? "Unknown"}.{func.Name}";
        _args = args;
        _startTimestampMicroseconds = _profiler.GetTimestamp();

        _profiler.BeginEvent(_name, _args);
    }

    public void Dispose() {
        long endTimestampMicroseconds = _profiler.GetTimestamp();
        long durationMicroseconds = endTimestampMicroseconds - _startTimestampMicroseconds;

        _profiler.EndEvent(_name, _args);
        _profiler.RecordCompleteEvent(_name, _startTimestampMicroseconds, durationMicroseconds, _args);
    }
}
