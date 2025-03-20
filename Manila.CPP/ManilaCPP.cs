namespace Shiron.Manila.CPP;

using Shiron.Manila.Ext;

public class ManilaCPP : ManilaPlugin {
	public ManilaCPP() : base("shiron.manila", "cpp", "1.0.0") {
	}

	public override void init() {
		debug("Init");
	}
	public override void release() {
		debug("Release");
	}
}
