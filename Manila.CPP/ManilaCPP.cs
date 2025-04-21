namespace Shiron.Manila.CPP;

using Shiron.Manila.API;
using Shiron.Manila.CPP.Components;
using Shiron.Manila.Ext;
using Shiron.Manila.Attributes;

public class ManilaCPP : ManilaPlugin {
    public ManilaCPP() : base("shiron.manila", "cpp", "1.0.0", "Shiron") {
    }

    [PluginInstance]
    public static ManilaCPP Instance { get; set; }

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
        if (project.HasComponent<StaticLibComponent>()) {
            Instance!.Debug("Building static library: " + project.Name);
            var comp = project.GetComponent<StaticLibComponent>();
            Instance.Debug("Building to: " + comp.BinDir!);
            return;
        }

        if (project.HasComponent<ConsoleComponent>()) {
            Instance!.Debug("Building console application: " + project.Name);
            var comp = project.GetComponent<ConsoleComponent>();
            Instance.Debug("Building to: " + comp.BinDir!);

            return;
        }
    }
}
