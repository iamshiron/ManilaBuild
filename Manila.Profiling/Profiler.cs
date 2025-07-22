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
    void RecordCompleteEvent(string name, long timestampMicroseconds, long durationMicroseconds, Dictionary<string, object>? args = null);
}

/// <summary>
/// Provides a simple API for profiling code execution using the Chrome Trace Event Format.
/// Events are collected and can be saved to a JSON file for visualization in chrome://tracing.
/// </summary>
public class Profiler(ILogger logger) : IProfiler {
    private readonly ILogger _logger = logger;
    private readonly List<TraceEvent> _events = [];
    private readonly Stopwatch _stopwatch = new();
    private readonly int _processId = Environment.ProcessId;
    private readonly Lock _lock = new();

    internal static readonly AsyncLocal<string?> _currentCategory = new();

    public void BeginEvent(string name, Dictionary<string, object>? args = null) {
        lock (_lock) {
            _events.Add(new TraceEvent {
                Name = name,
                Category = _currentCategory.Value ?? "default",
                Phase = "B", // Begin event
                Timestamp = GetTimestampMicroseconds(),
                ProcessId = _processId,
                ThreadId = Environment.CurrentManagedThreadId, // More modern way to get ThreadId
                Arguments = args ?? new Dictionary<string, object>()
            });
        }
    }

    public void EndEvent(string name, Dictionary<string, object>? args = null) {
        lock (_lock) {
            _events.Add(new TraceEvent {
                Name = name,
                Category = _currentCategory.Value ?? "default",
                Phase = "E", // End event
                Timestamp = GetTimestampMicroseconds(),
                ProcessId = _processId,
                ThreadId = Environment.CurrentManagedThreadId,
                Arguments = args ?? new Dictionary<string, object>()
            });
        }
    }

    public void RecordCompleteEvent(string name, long timestampMicroseconds, long durationMicroseconds, Dictionary<string, object>? args = null) {
        lock (_lock) {
            _events.Add(new TraceEvent {
                Name = name,
                Category = _currentCategory.Value ?? "default",
                Phase = "X", // Complete event
                Timestamp = timestampMicroseconds,
                Duration = durationMicroseconds,
                ProcessId = _processId,
                ThreadId = Environment.CurrentManagedThreadId,
                Arguments = args ?? new Dictionary<string, object>()
            });
        }
    }

    public long GetTimestampMicroseconds() {
        return _stopwatch.ElapsedTicks * 1_000_000 / Stopwatch.Frequency;
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

            try {
                var settings = new JsonSerializerSettings { Formatting = Formatting.None }; // Use no formatting for smaller file sizes
                string jsonString = JsonConvert.SerializeObject(_events, settings);
                File.WriteAllText(filePath, jsonString);
                _logger.Debug($"Profiling data saved to: {filePath}");
            } catch {
                throw;
            }
        }
    }
}

public readonly struct ProfileCategory : IDisposable {
    private readonly string? _previousCategory;

    public ProfileCategory(string categoryName) {
        _previousCategory = Profiler._currentCategory.Value;
        Profiler._currentCategory.Value = categoryName;
    }

    public readonly void Dispose() {
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
        _startTimestampMicroseconds = Utils.TimeNow();
    }

    public ProfileScope(IProfiler profiler, string name, Dictionary<string, object>? args = null) {
        _profiler = profiler;
        _name = name;
        _args = args;
        _startTimestampMicroseconds = Utils.TimeNow();
    }

    public ProfileScope(IProfiler profiler, MethodBase func, Dictionary<string, object>? args = null) {
        _profiler = profiler;
        _name = $"{func.DeclaringType?.FullName ?? "Unknown"}.{func.Name}";
        _args = args;
        _startTimestampMicroseconds = Utils.TimeNow();
    }

    public readonly void Dispose() {
        long endTimestampMicroseconds = Utils.TimeNow();
        long durationMicroseconds = endTimestampMicroseconds - _startTimestampMicroseconds;

        _profiler.RecordCompleteEvent(_name, _startTimestampMicroseconds, durationMicroseconds, _args);
    }
}
