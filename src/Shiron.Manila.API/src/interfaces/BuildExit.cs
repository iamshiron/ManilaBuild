
using Shiron.Manila.API.Interfaces.Artifacts;

namespace Shiron.Manila.API.Interfaces;

public interface IBuildExitCode { }

public class BuildExitCodeSuccess(ArtifactOutputBuilder builder) : IBuildExitCode {
    public readonly ArtifactOutput Outputs = builder.Build();
}
public class BuildExitCodeFailed(Exception e) : IBuildExitCode {
    public readonly Exception Exception = e;
}

public class BuildExitCodeCancelled : IBuildExitCode { }

public class BuildExitCodeCached(string cacheKey) : IBuildExitCode {
    public readonly string CacheKey = cacheKey;
}
