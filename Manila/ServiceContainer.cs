
using Shiron.Manila.Artifacts;
using Shiron.Manila.Caching;
using Shiron.Manila.Ext;
using Shiron.Manila.Logging;
using Shiron.Manila.Profiling;
using Shiron.Manila.Registries;
using Shiron.Manila.Utils;

namespace Shiron.Manila;

public class ServiceContainer(ILogger logger, IProfiler profiler, IJobRegistry jobRegistry,
            IArtifactManager artifactManager,
            IExtensionManager extensionManager,
            INuGetManager nuGetManager,
            IFileHashCache fileHashCache) {
    public readonly IJobRegistry JobRegistry = jobRegistry;
    public readonly IArtifactManager ArtifactManager = artifactManager;
    public readonly IExtensionManager ExtensionManager = extensionManager;
    public readonly INuGetManager NuGetManager = nuGetManager;
    public readonly IFileHashCache FileHashCache = fileHashCache;
    public readonly ILogger Logger = logger;
    public readonly IProfiler Profiler = profiler;
}
