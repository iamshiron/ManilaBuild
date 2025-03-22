namespace Shiron.Manila.CPP.Components;

using Shiron.Manila.API;
using Shiron.Manila.Attributes;
using Shiron.Manila.Attributes;

public class CppComponent : PluginComponent {
	public CppComponent(string name) : base(name) {
	}

	[ScriptProperty]
	public Dir binDir { get; set; }
	[ScriptProperty]
	public Dir objDir { get; set; }
	[ScriptProperty]
	public EToolChain toolChain { get; set; }
}
