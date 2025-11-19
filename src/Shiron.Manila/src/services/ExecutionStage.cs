using Shiron.Logging;
using Shiron.Manila.API.Interfaces;
using Shiron.Manila.Logging;

namespace Shiron.Manila.Services;

/// <summary>Mutable build stage tracker.</summary>
public class ExecutionStage(ILogger logger) : IExecutionStage {
    private readonly ILogger _logger = logger;
    private long _stageChangeTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// <summary>Current stage.</summary>
    public ExecutionStages Stage { get; private set; } = ExecutionStages.Setup;

    /// <summary>Transition to new stage.</summary>
    public void ChangeState(ExecutionStages newStage) {
        if (Stage == newStage) {
            _logger.Warning($"Execution stage is already set to {newStage}. No change made.");
            return;
        }

        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _logger.Log(new StageChangeLogEntry(Stage, newStage, _stageChangeTime));
        Stage = newStage;
        _stageChangeTime = currentTime;
    }
}
