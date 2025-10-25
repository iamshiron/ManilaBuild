
using Shiron.Manila.API.Interfaces;
using Shiron.Manila.Logging;
using Shiron.Manila.Profiling;

namespace Shiron.Manila.API.Utils;

public record APIServiceContainer(
    ILogger Logger,
    IProfiler Profiler,
    IExtensionManager ExtensionManager,
    IJobRegistry JobRegistry,
    IArtifactManager ArtifactManager,
    IArtifactCache ArtifactCache
);
