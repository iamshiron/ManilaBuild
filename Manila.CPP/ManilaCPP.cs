namespace Shiron.Manila.CPP;

using Shiron.Manila.API;
using Shiron.Manila.CPP.Components;
using Shiron.Manila.Attributes;
using Shiron.Manila.Utils;

public class ManilaCPP : ManilaPlugin {
	public ManilaCPP() : base("shiron.manila", "cpp", "1.0.0") {
	}

	[PluginInstance]
	public static ManilaCPP instance { get; set; }

	public override void init() {
		debug("Init");

		registerComponent(new Components.StaticLibComponent());
		registerComponent(new Components.ConsoleComponent());

		registerEnum(typeof(EToolChain));
	}
	public override void release() {
		debug("Release");
	}

	[ScriptFunction]
	public static void build(Workspace workspace, Project project, BuildConfig config) {
		if (project.hasComponent<StaticLibComponent>()) {
			instance.debug("Building static library: " + project.name);
			var comp = project.getComponent<StaticLibComponent>();
			instance.debug("Building to: " + comp.binDir);

			return;
		}

		if (project.hasComponent<ConsoleComponent>()) {
			instance.debug("Building console application: " + project.name);
			var comp = project.getComponent<ConsoleComponent>();
			instance.debug("Building to: " + comp.binDir);

			return;
		}
	}
}
