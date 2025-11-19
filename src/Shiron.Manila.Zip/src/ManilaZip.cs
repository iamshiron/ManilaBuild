using Shiron.Manila.API.Attributes;
using Shiron.Manila.API.Ext;
using Shiron.Manila.Zip.Artifacts;
using Shiron.Manila.Zip.Templates;
using Shiron.Utils;

namespace Shiron.Manila.Zip;

public class ManilaZip : ManilaPlugin {
    public ManilaZip() : base("shiron.manila", "zip", "1.0.0", ["Shiron"], []) {
    }

    [PluginInstance]
    public static ManilaZip? Instance { get; private set; }

    public override void Init() {
        Debug("Init");
        RegisterArtifact("zip", typeof(ZipArtifact));
        RegisterProjectTemplate(DefaultTemplate.Create());
        RegisterDependency<TestDependency>();
    }

    public override void Release() {
        Debug("Release");
    }
}
