using Shiron.Manila.API.Attributes;
using Shiron.Manila.API.Ext;
using Shiron.Manila.API.Interfaces;
using Shiron.Manila.JS.Artifacts;

namespace Shiron.Manila.JS;

public class ManilaJS : ManilaPlugin {
    public ManilaJS() : base("shiron.manila", "js", "1.0.0", ["Shiron"], []) {
    }

    [PluginInstance]
    public static ManilaJS? Instance { get; private set; }

    public override void Init() {
        Debug("Init");
        RegisterArtifact("js", typeof(JSArtifact));
        RegisterDependency<NPMDepndency>();
    }

    public override void Release() {
        Debug("Release");
    }

    public static string GetExecutableFromRuntime(Runtime runtime) {
        return runtime switch {
            Runtime.Node => "node",
            Runtime.Bun => "bun",
            _ => throw new ArgumentOutOfRangeException(nameof(runtime), $"Unsupported runtime: {runtime}")
        };
    }

    [ManilaExpose]
    public IDependency NPM(string package, string? version = null) {
        return new NPMDepndency(package, version);
    }
}
