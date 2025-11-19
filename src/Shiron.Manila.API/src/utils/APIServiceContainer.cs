
using Shiron.Logging;
using Shiron.Manila.API.Interfaces;
using Shiron.Profiling;

namespace Shiron.Manila.API.Interfaces;

public record APIServiceContainer(
    ILogger Logger,
    IProfiler Profiler,
    IExtensionManager ExtensionManager,
    IJobRegistry JobRegistry,
    IArtifactManager ArtifactManager
);
