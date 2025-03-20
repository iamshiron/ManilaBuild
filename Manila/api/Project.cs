namespace Shiron.Manila.API;

using System.Dynamic;
using System.Reflection;
using Microsoft.ClearScript;
using Microsoft.VisualBasic;
using Shiron.Manila.Attributes;
using Shiron.Manila.Ext;
using Shiron.Manila.Utils;

public class Project : DynamicObject, IScriptableObject {
	[ScriptProperty(true)]
	public string name { get; private set; }

	[ScriptProperty(true)]
	public Dir path { get; private set; }

	public string getIdentifier() {
		string relativeDir = Path.GetRelativePath(ManilaEngine.getInstance().root, path.path);
		return ":" + relativeDir.Replace(Path.DirectorySeparatorChar, ':').ToLower();
	}

	public Dictionary<Type, PluginComponent> pluginComponents { get; } = new();

	[ScriptProperty]
	public string? version { get; set; }
	[ScriptProperty]
	public string? group { get; set; }
	[ScriptProperty]
	public string? description { get; set; }

	public Dictionary<string, List<Delegate>> dynamicMethods { get; } = new();

	public Project(string name, string location) {
		this.name = name;
		this.path = new Dir(location);
	}

	public void addScriptProperty(PropertyInfo prop, object? obj = null) {
		if (obj == null) obj = this;

		Logger.debug($"Adding property '{prop.Name}' to script context.");
		var scriptPropertyInfo = prop.GetCustomAttribute<ScriptProperty>();
		if (scriptPropertyInfo == null) throw new Exception($"Property '{prop.Name}' is not a script property.");

		var setMethod = prop.GetSetMethod();

		if (setMethod != null && !scriptPropertyInfo.immutable)
			ManilaEngine.getInstance().currentContext.scriptEngine.AddHostObject(prop.Name, FunctionUtils.toDelegate(obj, prop.GetSetMethod()!));


		var methods = new List<Delegate> {
			FunctionUtils.toDelegate(obj, prop.GetGetMethod()!)
		};

		if (setMethod != null && !scriptPropertyInfo.immutable) methods.Add(FunctionUtils.toDelegate(obj, setMethod));

		dynamicMethods.Add(prop.Name, methods);
	}

	public void OnExposedToScriptCode(ScriptEngine engine) {
	}
	public override IEnumerable<string> GetDynamicMemberNames() {
		return dynamicMethods.Keys;
	}

	private bool sameParametes(MethodInfo method, object?[] args) {
		var methodParams = method.GetParameters();
		if (methodParams.Length != args.Length) return false;

		for (int i = 0; i < methodParams.Length; ++i)
			if (!methodParams[i].ParameterType.Equals(args[i].GetType())) return false;

		return true;
	}

	public override bool TryInvokeMember(InvokeMemberBinder binder, object?[] args, out object result) {
		if (dynamicMethods.TryGetValue(binder.Name, out var methods)) {
			Logger.debug($"Invoking method '{binder.Name}'");

			foreach (var method in methods) {
				if (!sameParametes(method.Method, args)) continue;

				var methodParams = method.Method.GetParameters();
				for (int i = 0; i < methodParams.Length; ++i) {
					var param = methodParams[i];
					Logger.debug($"Parameter: {param.Name}");

					// Convert enum strings to enum values
					if (param.ParameterType.IsEnum) {
						var type = param.ParameterType;
						args[i] = Enum.Parse(type, args[i].ToString());
					}
				}


				result = method.DynamicInvoke(args);
				return true;
			}
		}

		Logger.debug($"Method '{binder.Name}' not found.");
		return base.TryInvokeMember(binder, args, out result);
	}

	public void applyComponent(PluginComponent component) {
		Logger.debug($"Applying component '{component}'.");

		if (pluginComponents.ContainsKey(component.GetType())) {
			Logger.warn($"Component '{component}' already applied.");
			return;
		}
		pluginComponents.Add(component.GetType(), component);

		foreach (var e in component.plugin.enums) {
			ManilaEngine.getInstance().currentContext.applyEnum(e);
		}

		foreach (var prop in component.GetType().GetProperties()) {
			System.Console.WriteLine(prop);
			addScriptProperty(prop, component);
		}
	}
}
