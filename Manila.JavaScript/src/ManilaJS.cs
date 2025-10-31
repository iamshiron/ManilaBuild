using Shiron.Manila.API.Attributes;
using Shiron.Manila.API.Ext;

namespace Shiron.Manila.JS;

public class ManilaJS : ManilaPlugin {
    public ManilaJS() : base("shiron.manila", "js", "1.0.0", ["Shiron"], []) {
    }

    [PluginInstance]
    public static ManilaJS? Instance { get; private set; }

    public override void Init() {
        Debug("Init");
    }

    public override void Release() {
        Debug("Release");
    }
}
