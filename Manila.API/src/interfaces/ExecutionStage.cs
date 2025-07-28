
namespace Shiron.Manila.API.Interfaces;

public interface IExecutionStage {
    /// <summary>
    /// Gets the current execution stage of the application.
    /// </summary>
    ExecutionStages Stage { get; }
}

/// <summary>
/// Defines the distinct stages of the application's main process lifecycle.
/// </summary>
public enum ExecutionStages {
    /// <summary>
    /// The initial stage where the application is being set up. This may include
    /// initializing the environment, loading configurations, and preparing for discovery.
    /// </summary>
    Setup,

    /// <summary>
    /// The initial stage where components, plugins, or tasks are identified and cataloged.
    /// </summary>
    Discovery,

    /// <summary>
    /// The stage where discovered plugins or modules are loaded into memory.
    /// </summary>
    PluginLoading,

    /// <summary>
    /// The stage for setting up and validating configurations. This includes reading settings files,
    /// establishing connections, and preparing the environment based on loaded components.
    /// </summary>
    Configuration,

    /// <summary>
    /// The main execution stage where the application runs its core logic.
    /// This is where tasks are executed, jobs are processed, and the main functionality is performed.
    /// </summary>
    Runtime,

    /// <summary>
    /// The final stage for graceful shutdown. This includes releasing resources,
    /// closing connections, and performing cleanup tasks.
    /// </summary>
    Shutdown
}
