namespace Shiron.Manila;

using System.Dynamic;
using System.Reflection;
using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;
using Shiron.Manila.Attributes;
using Shiron.Manila.Ext;
using Shiron.Manila.Utils;

public sealed class ScriptContext {
	public readonly ScriptEngine scriptEngine;
	public ManilaEngine engine { get; private set; }
	public string scriptPath { get; private set; }
	public readonly API.Project project;

	public List<Type> enumComponents { get; } = new();

	public ScriptContext(ManilaEngine engine, API.Project project, string scriptPath) {
		scriptEngine = new V8ScriptEngine();
		this.engine = engine;
		this.scriptPath = scriptPath;
		this.project = project;
	}

	public void init() {
		scriptEngine.AddHostObject("Manila", new API.Manila(this));
		scriptEngine.AddHostObject("print", (params object[] args) => {
			Logger.scriptLog(args);
		});

		foreach (var prop in project.GetType().GetProperties()) {
			if (prop.GetCustomAttribute<ScriptProperty>() == null) continue;
			project.addScriptProperty(prop);
		}
		foreach (var func in project.GetType().GetMethods()) {
			if (func.GetCustomAttribute<ScriptFunction>() == null) continue;
			project.addScriptFunction(func, scriptEngine);
		}
	}
	public void execute() {
		try {
			scriptEngine.Execute(File.ReadAllText(scriptPath));
		} catch (ScriptEngineException e) {
			Logger.error("Error in script: " + scriptPath);
			Logger.info(e.Message);
			throw;
		}
	}

	public void applyEnum(Type t) {
		Logger.debug($"Applying enum '{t}'.");


		if (t.GetType().GetCustomAttributes<ScriptEnum>() == null) throw new Exception($"Object '{t}' is not a script enum.");

		if (enumComponents.Contains(t)) {
			Logger.warn($"Enum '{t}' already applied.");
			return;
		}

		enumComponents.Add(t);
		scriptEngine.AddHostType(t.Name[1..], t);
	}
}
