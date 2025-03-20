namespace Shiron.Manila.API;

using System.Dynamic;
using System.Reflection;
using Microsoft.ClearScript;
using Microsoft.VisualBasic;
using Shiron.Manila.Attributes;
using Shiron.Manila.Utils;

public class Project : DynamicObject, IScriptableObject {
	public string name { get; private set; }
	public Dir location { get; private set; }

	public string getIdentifier() {
		string relativeDir = Path.GetRelativePath(ManilaEngine.getInstance().root, location.path);
		return ":" + relativeDir.Replace(Path.DirectorySeparatorChar, ':').ToLower();
	}


	[ScriptProperty]
	public string? version { get; set; }
	[ScriptProperty]
	public string? group { get; set; }
	[ScriptProperty]
	public string? description { get; set; }

	public Dictionary<string, List<Delegate>> dynamicMethods { get; } = new();

	public Project(string name, string location) {
		this.name = name;
		this.location = new Dir(location);
	}

	public void addScriptProperty(PropertyInfo prop) {
		ManilaEngine.getInstance().currentContext.scriptEngine.AddHostObject(prop.Name, FunctionUtils.toDelegate(this, prop.GetSetMethod()!));

		dynamicMethods.Add(prop.Name, new List<Delegate> {
			FunctionUtils.toDelegate(this, prop.GetGetMethod()!),
			FunctionUtils.toDelegate(this, prop.GetSetMethod()!)
		});
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
}
