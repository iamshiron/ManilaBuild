
using Shiron.Manila.Logging;

namespace Shiron.Manila.Enums;

public class ExecutionStage(ILogger logger) : IExecutionStage {
    private readonly ILogger _logger = logger;
    private long _stageChangeTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public ExecutionStages Stage { get; private set; } = ExecutionStages.Setup;

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
