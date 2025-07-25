
using Shiron.Manila.Artifacts;
using Shiron.Manila.Caching;
using Shiron.Manila.Enums;
using Shiron.Manila.Ext;
using Shiron.Manila.Logging;
using Shiron.Manila.Profiling;
using Shiron.Manila.Registries;
using Shiron.Manila.Utils;

namespace Shiron.Manila;

/// <summary>
/// Base container for services used by the Manila engine.
/// </summary>
public record BaseServiceContainer(
            ILogger Logger,
            IProfiler Profiler,
            IExecutionStage ExecutionStage
);

/// <summary>
/// Container for all services used by the Manila engine.
/// </summary>
public record ServiceContainer(IJobRegistry JobRegistry,
            IArtifactManager ArtifactManager,
            IExtensionManager ExtensionManager,
            INuGetManager NuGetManager,
            IFileHashCache FileHashCache
);
