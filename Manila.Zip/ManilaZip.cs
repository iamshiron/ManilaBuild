using Shiron.Manila.Attributes;
using Shiron.Manila.Ext;

namespace Manila.Zip;

public class ManilaZip : ManilaPlugin {
    public ManilaZip() : base("shiron.manila", "zip", "1.0.0", ["Shiron"], []) {
    }

    [PluginInstance]
    public static ManilaZip? Instance { get; private set; }

    public override void Init() {
        Debug("Init");
    }

    public override void Release() {
        Debug("Release");
    }
}
