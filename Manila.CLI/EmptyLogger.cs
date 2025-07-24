

using System.Diagnostics.CodeAnalysis;
using Shiron.Manila.Logging;

namespace Shiron.Manila.CLI;

public class EmptyLogger : ILogger {
    public string? LoggerPrefix => null;
    public LogContext LogContext => new();

#pragma warning disable CS0067
    public event Action<ILogEntry>? OnLogEntry;
#pragma warning restore CS0067

    public void AddInjector(Guid id, LogInjector injector) { }
    public void Critical(string message) { }
    public void Debug(string message) { }
    public void Error(string message) { }
    public void Info(string message) { }
    public void Log(ILogEntry entry) { }
    public void RemoveInjector(Guid id) { }
    public void System(string message) { }
    public void Warning(string message) { }
}
