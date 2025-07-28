
using System.Diagnostics.CodeAnalysis;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Logging;

namespace Shiron.Manila.Mock;

public class MockLogger : ILogger {
    public string? LoggerPrefix => "MockLogger";

    public LogContext LogContext => new();

#pragma warning disable CS0414
    // The field 'MockLogger.OnLogEntry' is assigned but its value is never used
    public event Action<ILogEntry>? OnLogEntry = null;
#pragma warning restore CS0414

    public void AddInjector(Guid id, LogInjector injector) => throw new ManilaException();

    public void Debug(string message) => Console.WriteLine($"[DEBUG] {message}");
    public void System(string message) => Console.WriteLine($"[SYSTEM] {message}");
    public void Info(string message) => Console.WriteLine($"[INFO] {message}");
    public void Warning(string message) => Console.WriteLine($"[WARNING] {message}");
    public void Error(string message) => Console.WriteLine($"[ERROR] {message}");
    public void Critical(string message) => Console.WriteLine($"[CRITICAL] {message}");

    public void Log(ILogEntry entry) {
        Console.WriteLine($"[{entry.Level}] {entry}");
    }
    public void RemoveInjector(Guid id) => throw new ManilaException();
    public void MarkupLine(string message, bool logAlways = false) { }
}

public class EmptyMockLogger : ILogger {
    public string? LoggerPrefix => null;

    public LogContext LogContext => new();

#pragma warning disable CS0414
    public event Action<ILogEntry>? OnLogEntry = null;
#pragma warning restore CS0414

    public void AddInjector(Guid id, LogInjector injector) { }
    public void RemoveInjector(Guid id) { }

    public void Debug(string message) { }
    public void System(string message) { }
    public void Info(string message) { }
    public void Warning(string message) { }
    public void Error(string message) { }
    public void Critical(string message) { }

    public void Log(ILogEntry entry) { }

    public void MarkupLine(string message, bool logAlways = false) { }
}
