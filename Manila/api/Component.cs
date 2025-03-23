namespace Shiron.Manila.API;

using Microsoft.ClearScript;
using System.Dynamic;
using System.Reflection;
using Microsoft.ClearScript;
using Shiron.Manila.Attributes;
using Shiron.Manila.Utils;

public class Component : DynamicObject, IScriptableObject {
	[ScriptProperty(true)]
	public Dir path { get; private set; }

	public Dictionary<Type, PluginComponent> pluginComponents { get; } = new();
	public List<Type> plugins { get; } = new();
	public Dictionary<string, List<Delegate>> dynamicMethods { get; } = new();
	public List<Task> tasks { get; } = new();

	public virtual string getIdentifier() {
		string relativeDir = Path.GetRelativePath(ManilaEngine.getInstance().root, path.path);
		return ":" + relativeDir.Replace(Path.DirectorySeparatorChar, ':').ToLower();
	}

	public Component(string path) {
		this.path = new Dir(path);
	}

	public void OnExposedToScriptCode(ScriptEngine engine) {
	}
	public override IEnumerable<string> GetDynamicMemberNames() {
		return dynamicMethods.Keys;
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
	public void addScriptFunction(MethodInfo prop, ScriptEngine engine, object? obj = null) {
		if (obj == null) obj = this;

		Logger.debug($"Adding function '{prop.Name}' to script context.");
		var scriptFunctionInfo = prop.GetCustomAttribute<ScriptFunction>();
		if (scriptFunctionInfo == null) throw new Exception($"Method '{prop.Name}' is not a script function.");

		var methods = dynamicMethods.ContainsKey(prop.Name) ? dynamicMethods[prop.Name] : new List<Delegate>();
		methods.Add(FunctionUtils.toDelegate(obj, prop));
		dynamicMethods[prop.Name] = methods;

		engine.AddHostObject(prop.Name, FunctionUtils.toDelegate(obj, prop));

		Logger.debug($"Added function '{prop.Name}' to script context.");
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

		applyPlugin(component.plugin);

		foreach (var prop in component.GetType().GetProperties()) {
			addScriptProperty(prop, component);
		}
	}

	public void applyPlugin(ManilaPlugin plugin) {
		if (plugins.Contains(plugin.GetType())) {
			Logger.warn($"Plugin '{plugin}' already applied.");
			return;
		}

		plugins.Add(plugin.GetType());

		ManilaEngine.getInstance().currentContext.scriptEngine.AddHostType(plugin.GetType().Name, plugin.GetType());
	}

	public bool hasComponent<T>() where T : PluginComponent {
		return pluginComponents.ContainsKey(typeof(T));
	}
	public T getComponent<T>() where T : PluginComponent {
		if (!pluginComponents.ContainsKey(typeof(T))) throw new Exception($"Component '{typeof(T)}' not found.");
		return (T) pluginComponents[typeof(T)];
	}
}
