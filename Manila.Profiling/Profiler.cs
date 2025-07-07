using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Shiron.Manila.Logging;

namespace Shiron.Manila.Profiling;

/// <summary>
/// Provides a simple API for profiling code execution using the Chrome Trace Event Format.
/// Events are collected and can be saved to a JSON file for visualization in chrome://tracing.
/// </summary>
public static class Profiler {
    private static readonly List<TraceEvent> _events = [];
    private static readonly Stopwatch _stopwatch = new();
    private static bool _isEnabled = false;
    private static readonly int _processId = Environment.ProcessId;
    private static readonly Lock _lock = new();

    internal static readonly AsyncLocal<string?> _currentCategory = new();

    /// <summary>
    /// Gets or sets a value indicating whether profiling is currently enabled.
    /// </summary>
    public static bool IsEnabled {
        get => _isEnabled;
        set {
            if (_isEnabled == value) return;

            _isEnabled = value;
            if (_isEnabled) {
                _stopwatch.Restart();
            } else {
                _stopwatch.Stop();
            }
        }
    }

    /// <summary>
    /// Records the beginning of a profiling event.
    /// </summary>
    /// <param name="name">The name of the event (e.g., function name).</param>
    /// <param name="args">Optional arguments to associate with the event.</param>
    public static void BeginEvent(string name, Dictionary<string, object>? args = null) {
        if (!IsEnabled) return;

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

    /// <summary>
    /// Records the end of a profiling event.
    /// </summary>
    /// <param name="name">The name of the event that is ending.</param>
    /// <param name="args">Optional arguments to associate with the event.</param>
    public static void EndEvent(string name, Dictionary<string, object>? args = null) {
        if (!IsEnabled) return;

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

    /// <summary>
    /// Records a complete event, which has both a beginning and an end, and a duration.
    /// This is often more convenient for profiling function calls.
    /// </summary>
    /// <param name="name">The name of the event.</param>
    /// <param name="timestampMicroseconds">The start timestamp of the event in microseconds.</param>
    /// <param name="durationMicroseconds">The duration of the event in microseconds.</param>
    /// <param name="args">Optional arguments to associate with the event.</param>
    internal static void RecordCompleteEvent(string name, long timestampMicroseconds, long durationMicroseconds, Dictionary<string, object>? args = null) {
        if (!IsEnabled) return;

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

    /// <summary>
    /// Gets the current timestamp in microseconds since the profiler started.
    /// </summary>
    /// <returns>The timestamp in microseconds.</returns>
    public static long GetTimestampMicroseconds() {
        return _stopwatch.ElapsedTicks * 1_000_000 / Stopwatch.Frequency;
    }

    /// <summary>
    /// Clears all collected profiling events.
    /// </summary>
    public static void ClearEvents() {
        lock (_lock) {
            _events.Clear();
            _stopwatch.Reset(); // Reset the stopwatch as well
        }
    }

    /// <summary>
    /// Saves all collected profiling events to a JSON file in the Chrome Trace Event Format.
    /// </summary>
    /// <param name="filePath">The path to the output JSON file.</param>
    public static void SaveToFile(string baseDir) {
        if (!IsEnabled) {
            Logger.Debug("Profiler is not enabled. No data to save.");
            return;
        }

        lock (_lock) {
            if (!Directory.Exists(baseDir)) {
                try {
                    Directory.CreateDirectory(baseDir);
                } catch {
                    throw;
                }
            }

            string timestamp = DateTime.Now.ToString("yyyy-MM_dd-HH_mm_ss");
            string fileName = $"profile-{timestamp}.json";
            string filePath = Path.Combine(baseDir, fileName);

            try {
                var settings = new JsonSerializerSettings { Formatting = Formatting.None }; // Use no formatting for smaller file sizes
                string jsonString = JsonConvert.SerializeObject(_events, settings);
                File.WriteAllText(filePath, jsonString);
                Logger.Info($"Profiling data saved to: {filePath}");
            } catch {
                throw;
            }
        }
    }
}

/// <summary>
/// A disposable helper struct for defining a profiling category for a scope of code.
/// Use with a 'using' statement to set the category for all nested ProfileScope events.
/// </summary>
public readonly struct ProfileCategory : IDisposable {
    private readonly string? _previousCategory;
    private readonly bool _profilerEnabled;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileCategory"/> struct,
    /// setting the current profiling category for the duration of its scope.
    /// </summary>
    /// <param name="categoryName">The name of the category to set.</param>
    public ProfileCategory(string categoryName) {
        _profilerEnabled = Profiler.IsEnabled;
        if (_profilerEnabled) {
            _previousCategory = Profiler._currentCategory.Value;
            Profiler._currentCategory.Value = categoryName;
        } else {
            _previousCategory = null;
        }
    }

    /// <summary>
    /// Disposes the scope, restoring the previous profiling category.
    /// </summary>
    public readonly void Dispose() {
        if (_profilerEnabled) {
            Profiler._currentCategory.Value = _previousCategory;
        }
    }
}

/// <summary>
/// A disposable helper struct for profiling a scope of code (e.g., a function or a custom block).
/// Use with a 'using' statement to automatically record the duration of the scope.
/// The category is determined by the active <see cref="ProfileCategory"/> scope.
/// </summary>
public readonly struct ProfileScope : IDisposable {
    private readonly string _name;
    private readonly long _startTimestampMicroseconds;
    private readonly Dictionary<string, object>? _args;
    private readonly bool _profilerEnabled;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileScope"/> struct.
    /// When this struct is disposed, it records a complete event with the duration.
    /// The name of the calling method is automatically captured.
    /// </summary>
    /// <param name="args">Optional arguments to associate with the event.</param>
    /// <param name="name">This parameter is automatically populated by the compiler
    /// with the name of the calling member. Do not provide a value for this parameter.</param>
    public ProfileScope(Dictionary<string, object>? args = null, [CallerMemberName] string name = "") {
        _args = args;
        _profilerEnabled = Profiler.IsEnabled;
        _name = name; // Captured automatically by CallerMemberName

        _startTimestampMicroseconds = _profilerEnabled ? Profiler.GetTimestampMicroseconds() : 0;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileScope"/> struct with a custom name.
    /// When this struct is disposed, it records a complete event with the duration.
    /// </summary>
    /// <param name="name">The custom name for this profiling scope.</param>
    /// <param name="args">Optional arguments to associate with the event.</param>
    public ProfileScope(string name, Dictionary<string, object>? args = null) {
        _name = name; // Use the provided custom name
        _args = args;
        _profilerEnabled = Profiler.IsEnabled;

        _startTimestampMicroseconds = _profilerEnabled ? Profiler.GetTimestampMicroseconds() : 0;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileScope"/> struct, deriving the name from a <see cref="MethodBase"/>.
    /// </summary>
    /// <param name="func">The method to derive the name from.</param>
    /// <param name="args">Optional arguments to associate with the event.</param>
    public ProfileScope(MethodBase func, Dictionary<string, object>? args = null) {
        _name = $"{func.DeclaringType?.FullName ?? "Unknown"}.{func.Name}";
        _args = args;
        _profilerEnabled = Profiler.IsEnabled;

        _startTimestampMicroseconds = _profilerEnabled ? Profiler.GetTimestampMicroseconds() : 0;
    }

    /// <summary>
    /// Disposes the scope, recording the complete event.
    /// </summary>
    public readonly void Dispose() {
        if (_profilerEnabled) {
            long endTimestampMicroseconds = Profiler.GetTimestampMicroseconds();
            long durationMicroseconds = endTimestampMicroseconds - _startTimestampMicroseconds;

            Profiler.RecordCompleteEvent(_name, _startTimestampMicroseconds, durationMicroseconds, _args);
        }
    }
}
