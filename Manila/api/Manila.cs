namespace Shiron.Manila.API;

/// <summary>
/// The main Manila API class. Used for global functions.
/// </summary>
public sealed class Manila {
	private ScriptContext context;

	public Manila(ScriptContext context) {
		this.context = context;
	}

	public void log(string msg) {
		Console.WriteLine(msg);
	}
}
