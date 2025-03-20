namespace Shiron.Manila.Ext;

using System.Reflection;
using System.Text.RegularExpressions;
using Shiron.Manila.Utils;

public class ExtensionManager {
	private static readonly ExtensionManager instance = new ExtensionManager();

	public static readonly string DEFAULT_GROUP = "shiron.manila";

	public static readonly Regex pluginPattern = new(@"(?<group>[\w.\d]+):(?<name>[\w.\d]+)(?:@(?<version>[\w.\d]+))?");
	public static readonly Regex componentPattern = new(@"(?<group>[\w.\d]+):(?<name>[\w.\d]+)(?:@(?<version>[\w.\d]+))?:(?<component>[\w.\d]+)");

	public static ExtensionManager getInstance() {
		return instance;
	}

	public string? pluginDir { get; private set; }
	public List<ManilaPlugin> plugins = new();

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
					plugins.Add(plugin);

					foreach (var prop in type.GetProperties())
						if (prop.GetCustomAttribute<PluginInstance>() != null)
							prop.SetValue(null, plugin);
				}
			}
		}
	}

	public void initPlugins() {
		foreach (var plugin in plugins) {
			plugin.init();
		}
	}
	public void releasePlugins() {
		foreach (var plugin in plugins) {
			plugin.release();
		}
	}

	public ManilaPlugin getPlugin(string group, string name, string? version = null) {
		if (version == String.Empty) version = null;
		foreach (var plugin in plugins) {
			if (plugin.group == group && plugin.name == name && (version == null || plugin.version == version)) return plugin;
		}
		throw new Exception("Plugin not found: " + group + ":" + name + (version == null ? "" : "." + version));
	}
	public PluginComponent getPluginComponent(string group, string name, string component, string? version = null) {
		return getPlugin(group, name, version).getComponent(component);
	}

	public ManilaPlugin getPlugin(string key) {
		var match = pluginPattern.Match(key);
		if (!match.Success) throw new Exception("Invalid plugin key: " + key);
		return getPlugin(match.Groups["group"].Value, match.Groups["name"].Value, match.Groups["version"].Value);
	}
	public PluginComponent getPluginComponent(string key) {
		var match = componentPattern.Match(key);
		if (!match.Success) throw new Exception("Invalid component key: " + key);
		return getPluginComponent(match.Groups["group"].Value, match.Groups["name"].Value, match.Groups["component"].Value, match.Groups["version"].Value);
	}
}
