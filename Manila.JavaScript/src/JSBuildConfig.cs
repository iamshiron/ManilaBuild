
using Shiron.Manila.API;

namespace Shiron.Manila.JS;

public enum Runtime {
    Node,
    Bun
}

public class JSBuildConfig : BuildConfig {
    /// <summary>The JavaScript runtime to use for the build.</summary>
    public Runtime Runtime { get; set; } = Runtime.Node;

    public void SetRuntime(string runtime) {
        if (Enum.TryParse<Runtime>(runtime, true, out var parsedRuntime)) {
            Runtime = parsedRuntime;
            return;
        }
        throw new ArgumentException($"Invalid runtime: {runtime}");
    }
    public Runtime GetRuntime() {
        return Runtime;
    }
}
