
using Shiron.Manila.API.Interfaces.Artifacts;

namespace Shiron.Manila.Interfaces;

public interface IBuildExitCode { }

public class BuildExitCodeSuccess(IArtifactOutput[] outputs) : IBuildExitCode {
    public readonly IArtifactOutput[] Outputs = outputs;
}
public class BuildExitCodeFailed(Exception e) : IBuildExitCode {
    public readonly Exception Exception = e;
}

public class BuildExitCodeCancelled : IBuildExitCode { }

public class BuildExitCodeCached(string cacheKey) : IBuildExitCode {
    public readonly string CacheKey = cacheKey;
}
