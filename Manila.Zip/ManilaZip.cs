using Shiron.Manila.Attributes;
using Shiron.Manila.Ext;
using Shiron.Manila.Zip.Components;

namespace Shiron.Manila.Zip;

public class ManilaZip : ManilaPlugin {
    public ManilaZip() : base("shiron.manila", "zip", "1.0.0", ["Shiron"], []) {
    }

    [PluginInstance]
    public static ManilaZip? Instance { get; private set; }

    public override void Init() {
        Debug("Init");
        RegisterComponent(new ZipComponent());
    }

    public override void Release() {
        Debug("Release");
    }
}
