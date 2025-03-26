namespace Shiron.Manila.CPP;

using Shiron.Manila.API;
using Shiron.Manila.CPP.Components;
using Shiron.Manila.Attributes;
using Shiron.Manila.Utils;

public class ManilaCPP : ManilaPlugin {
    public ManilaCPP() : base("shiron.manila", "cpp", "1.0.0") {
    }

    [PluginInstance]
    public static ManilaCPP? instance { get; set; }

    public override void Init() {
        Debug("Init");

        RegisterComponent(new StaticLibComponent());
        RegisterComponent(new ConsoleComponent());
        RegisterEnum<EToolChain>();
    }
    public override void Release() {
        Debug("Release");
    }

    [ScriptFunction]
    public static void Build(Workspace workspace, Project project, BuildConfig config) {
        if (project.HasComponent<StaticLibComponent>()) {
            instance!.Debug("Building static library: " + project.Name);
            var comp = project.getComponent<StaticLibComponent>();
            instance.Debug("Building to: " + comp.BinDir!);
            return;
        }

        if (project.HasComponent<ConsoleComponent>()) {
            instance!.Debug("Building console application: " + project.Name);
            var comp = project.getComponent<ConsoleComponent>();
            instance.Debug("Building to: " + comp.BinDir!);

            return;
        }
    }
}
