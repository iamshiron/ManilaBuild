using Shiron.Manila.Utils;

namespace Shiron.Manila.Ext;


public abstract class ManilaPlugin {
	public readonly string group;
	public readonly string name;
	public readonly string version;

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
}
