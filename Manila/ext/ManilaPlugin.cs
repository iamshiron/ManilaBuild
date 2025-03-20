using Shiron.Manila.Utils;

namespace Shiron.Manila.Ext;


public abstract class ManilaPlugin {
	public readonly string group;
	public readonly string name;
	public readonly string version;
	public readonly Dictionary<string, PluginComponent> components = new();

	public ManilaPlugin(string group, string name, string version) {
		this.group = group;
		this.name = name;
		this.version = version;
	}

	public abstract void init();
	public abstract void release();

	public void debug(params object[] args) { Logger.pluginDebug(this, args); }
	public void info(params object[] args) { Logger.pluginInfo(this, args); }
	public void warn(params object[] args) { Logger.pluginWarn(this, args); }
	public void error(params object[] args) { Logger.pluginError(this, args); }

	public void registerComponent(PluginComponent component) {
		if (components.ContainsKey(component.name)) throw new Exception("Component with name " + component.name + " already registered");
		components.Add(component.name, component);
	}

	public PluginComponent getComponent(string name) {
		if (!components.ContainsKey(name)) throw new Exception("Component with name " + name + " not registered");
		return components[name];
	}

	public override string ToString() {
		return $"ManilaPlugin({group}:{name}@{version})";
	}
}
