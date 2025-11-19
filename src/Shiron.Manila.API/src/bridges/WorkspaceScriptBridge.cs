
using Shiron.Logging;
using Shiron.Profiling;

namespace Shiron.Manila.API.Bridges;

public class WorkspaceScriptBridge(ILogger logger, IProfiler profiler, Workspace workspace) : ScriptBridge {
    private readonly ILogger _logger = logger;
    private readonly IProfiler _profiler = profiler;
    internal readonly Workspace _handle = workspace;

    public DirHandle GetPath() {
        return new(_handle.Path);
    }
}
