namespace Shiron.Manila.Ext;

public abstract class PluginComponent {
	public readonly string name;

	public PluginComponent(string name) {
		this.name = name;
	}

	public override string ToString() {
		return $"PluginComponent({name})";
	}
}
