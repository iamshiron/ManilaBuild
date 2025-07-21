
using Shiron.Manila.Logging;

namespace Shiron.Manila.Mock;

public class MockLogger : ILogger {
    public string? LoggerPrefix => "MockLogger";

    public LogContext LogContext => new();

    public event Action<ILogEntry>? OnLogEntry;

    public void AddInjector(Guid id, LogInjector injector) => throw new NotImplementedException();

    public void Debug(string message) => Console.WriteLine($"[DEBUG] {message}");
    public void System(string message) => Console.WriteLine($"[SYSTEM] {message}");
    public void Info(string message) => Console.WriteLine($"[INFO] {message}");
    public void Warning(string message) => Console.WriteLine($"[WARNING] {message}");
    public void Error(string message) => Console.WriteLine($"[ERROR] {message}");
    public void Critical(string message) => Console.WriteLine($"[CRITICAL] {message}");

    public void Log(ILogEntry entry) {
        Console.WriteLine($"[{entry.Level}] {entry}");
    }
    public void RemoveInjector(Guid id) => throw new NotImplementedException();

}
