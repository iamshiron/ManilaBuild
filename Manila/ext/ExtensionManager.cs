namespace Shiron.Manila.Ext;

using System.Reflection;
using Shiron.Manila.Utils;

public class ExtensionManager {
	private static readonly ExtensionManager instance = new ExtensionManager();

	public static ExtensionManager getInstance() {
		return instance;
	}

	public string? pluginDir { get; private set; }
	public Dictionary<string, ManilaPlugin> plugins = new();

	public void init(string pluginDir) {
		this.pluginDir = pluginDir;
	}
	public void loadPlugins() {
		if (pluginDir == null) throw new Exception("Plugin directory not set");

		if (!Directory.Exists(pluginDir)) {
			Logger.warn("Plugin directory does not exist: " + pluginDir);
			Logger.info("Skipping plugin loading");
			return;
		}

		foreach (var file in Directory.GetFiles(pluginDir, "*.dll")) {
			var assembly = Assembly.LoadFile(Path.Join(Directory.GetCurrentDirectory(), file));
			foreach (var type in assembly.GetTypes()) {
				if (type.IsSubclassOf(typeof(ManilaPlugin))) {
					var plugin = (ManilaPlugin?) Activator.CreateInstance(type);
					if (plugin == null) throw new Exception("Failed to create plugin instance of type " + type + " loaded from " + file);
					plugins.Add(plugin.group + ":" + plugin.name, plugin);
				}
			}
		}
	}

	public void initPlugins() {
		foreach (var plugin in plugins.Values) {
			plugin.init();
		}
	}
	public void releasePlugins() {
		foreach (var plugin in plugins.Values) {
			plugin.release();
		}
	}
}
