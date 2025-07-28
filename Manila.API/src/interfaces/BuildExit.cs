
namespace Shiron.Manila.Interfaces;

public interface IBuildExitCode { }

public class BuildExitCodeSuccess : IBuildExitCode { }
public class BuildExitCodeFailed(Exception e) : IBuildExitCode {
    public readonly Exception Exception = e;
}

public class BuildExitCodeCancelled : IBuildExitCode { }

public class BuildExitCodeCached(string cacheKey) : IBuildExitCode {
    public readonly string CacheKey = cacheKey;
}
