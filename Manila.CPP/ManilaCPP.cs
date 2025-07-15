
using Shiron.Manila.API;
using Shiron.Manila.Attributes;
using Shiron.Manila.CPP.Components;
using Shiron.Manila.Ext;

namespace Shiron.Manila.CPP;
public class ManilaCPP : ManilaPlugin {
    public ManilaCPP() : base("shiron.manila", "cpp", "1.0.0", ["Shiron"], []) {
    }

    [PluginInstance]
    public static ManilaCPP? Instance { get; set; }

    public override void Init() {
        Debug("Init");

        RegisterComponent(new StaticLibComponent());
        RegisterComponent(new ConsoleComponent());
        RegisterEnum<EToolChain>();
        RegisterDependency<DependencyLink>();
        RegisterDependency<DependencyProject>();
    }
    public override void Release() {
        Debug("Release");
    }

    [ScriptFunction]
    public static void Build(Workspace workspace, Project project, BuildConfig config) {
        throw new NotImplementedException();
    }
}
