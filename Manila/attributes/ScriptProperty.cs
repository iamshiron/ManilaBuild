namespace Shiron.Manila.Attributes;

public class ScriptProperty : Attribute {
	public readonly bool immutable;

	public ScriptProperty(bool immutable = false) {
		this.immutable = immutable;
	}
}
