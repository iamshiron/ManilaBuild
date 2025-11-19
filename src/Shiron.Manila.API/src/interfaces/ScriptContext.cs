
using Shiron.Manila.API.Bridges;

namespace Shiron.Manila.API.Interfaces;

public interface IScriptContext {
    string ScriptPath { get; }
    List<Type> EnumComponents { get; }
    string GetCompiledFilePath();
    void Init(API.Manila manilaAPI, ScriptBridge bridge, Component component);
    string? GetEnvironmentVariable(string key);
    void SetEnvironmentVariable(string key, string value);
    Task ExecuteAsync(IFileHashCache cache, Component component);
    Guid ContextID { get; }
}
