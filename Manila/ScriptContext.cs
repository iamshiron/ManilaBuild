namespace Shiron.Manila;

using System.Reflection;
using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;

public sealed class ScriptContext {
	private ScriptEngine scriptEngine;
	public ManilaEngine engine { get; private set; }
	public string scriptPath { get; private set; }

	public ScriptContext(ManilaEngine engine, string scriptPath) {
		scriptEngine = new V8ScriptEngine();
		this.engine = engine;
		this.scriptPath = scriptPath;
	}

	public void init() {
		scriptEngine.AddHostObject("Manila", new API.Manila(this));
	}
	public void execute() {
		try {
			scriptEngine.Execute(File.ReadAllText(scriptPath));
		} catch (ScriptEngineException e) {
			Console.WriteLine("Error in script: " + scriptPath);
			Console.WriteLine(e.Message);
			throw;
		}
	}
}
