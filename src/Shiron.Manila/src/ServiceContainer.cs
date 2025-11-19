
using Shiron.Logging;
using Shiron.Manila.API.Interfaces;
using Shiron.Manila.Registries;
using Shiron.Profiling;
using Shiron.Utils;

namespace Shiron.Manila;

/// <summary>Core shared services.</summary>
public record BaseServiceContainer(
            ILogger Logger,
            IProfiler Profiler,
            IExecutionStage ExecutionStage
);

/// <summary>Extended engine services.</summary>
public record ServiceContainer(IJobRegistry JobRegistry,
            IArtifactManager ArtifactManager,
            IExtensionManager ExtensionManager,
            INuGetManager NuGetManager,
            IFileHashCache FileHashCache
);
