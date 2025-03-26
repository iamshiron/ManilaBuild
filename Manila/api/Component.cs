using Microsoft.ClearScript;
using System.Dynamic;
using System.Reflection;
using Shiron.Manila.Attributes;
using Shiron.Manila.Utils;

namespace Shiron.Manila.API;

/// <summary>
/// Represents a component in the build script. Components are used to group tasks and plugins. Can either be a workspace or a project.
/// </summary>
public class Component : DynamicObject, IScriptableObject {
    [ScriptProperty(true)]
    public Dir Path { get; private set; }

    public Dictionary<Type, PluginComponent> PluginComponents { get; } = [];
    public List<Type> plugins { get; } = [];
    public Dictionary<string, List<Delegate>> DynamicMethods { get; } = [];
    public List<Task> tasks { get; } = [];

    /// <summary>
    /// The GetIdentifier method returns a string that uniquely identifies the component.
    /// </summary>
    /// <returns>The identifier</returns>
    public virtual string GetIdentifier() {
        string relativeDir = System.IO.Path.GetRelativePath(ManilaEngine.GetInstance().Root, Path.path);
        return ":" + relativeDir.Replace(System.IO.Path.DirectorySeparatorChar, ':').ToLower();
    }

    public Component(string path) {
        this.Path = new Dir(path);
    }

    public void OnExposedToScriptCode(ScriptEngine engine) {
    }
    public override IEnumerable<string> GetDynamicMemberNames() {
        return DynamicMethods.Keys;
    }
    /// <summary>
    /// Add a property that gets exposed to the script context.
    /// </summary>
    /// <param name="prop">The property</param>
    /// <param name="obj">The instance of the class</param>
    /// <exception cref="Exception">If property wasn't attributed with <see cref="ScriptProperty"/></exception>
    public void AddScriptProperty(PropertyInfo prop, object? obj = null) {
        if (obj == null) obj = this;

        var propertyName = prop.Name;
        var scriptPropertyName = propertyName[0].ToString().ToLower() + propertyName[1..]; // Convert first letter to lowercase for JS naming convention
        var scriptPropertyGetterName = "get" + propertyName;
        var scriptPropertySetterName = "set" + propertyName;

        Logger.debug($"Adding property {propertyName}...");
        Logger.debug($"Script property name: {scriptPropertyName}");
        Logger.debug($"Script property setter name: {scriptPropertySetterName}");
        Logger.debug($"Script property getter name: {scriptPropertyGetterName}");

        var scriptPropertyInfo = prop.GetCustomAttribute<ScriptProperty>();
        if (scriptPropertyInfo == null) throw new Exception($"Property '{prop.Name}' is not a script property.");

        var setMethod = prop.GetSetMethod();

        if (setMethod != null && !scriptPropertyInfo.immutable) {
            ManilaEngine.GetInstance().CurrentContext!.ScriptEngine.AddHostObject(scriptPropertyName, FunctionUtils.ToDelegate(obj, prop.GetSetMethod()!));
        }


        var setterMethods = DynamicMethods.ContainsKey(scriptPropertySetterName) ? DynamicMethods[scriptPropertySetterName] : [];
        var getterMethods = DynamicMethods.ContainsKey(scriptPropertyGetterName) ? DynamicMethods[scriptPropertyGetterName] : [];

        if (setMethod != null && !scriptPropertyInfo.immutable) setterMethods.Add(FunctionUtils.ToDelegate(obj, setMethod));
        getterMethods.Add(FunctionUtils.ToDelegate(obj, prop.GetGetMethod()!));

        DynamicMethods[scriptPropertySetterName] = setterMethods;
        DynamicMethods[scriptPropertyGetterName] = getterMethods;
    }
    public void AddScriptFunction(MethodInfo prop, ScriptEngine engine, object? obj = null) {
        if (obj == null) obj = this;

        Logger.debug($"Adding function '{prop.Name}' to script context.");
        var scriptFunctionInfo = prop.GetCustomAttribute<ScriptFunction>();
        if (scriptFunctionInfo == null) throw new Exception($"Method '{prop.Name}' is not a script function.");

        var methods = DynamicMethods.ContainsKey(prop.Name) ? DynamicMethods[prop.Name] : new List<Delegate>();
        methods.Add(FunctionUtils.ToDelegate(obj, prop));
        DynamicMethods[prop.Name] = methods;

        engine.AddHostObject(prop.Name, FunctionUtils.ToDelegate(obj, prop));

        Logger.debug($"Added function '{prop.Name}' to script context.");
    }
    /// <summary>
    /// Check if the method has the same parameters as the arguments.
    /// </summary>
    /// <param name="method">The method to check</param>
    /// <param name="args">The lisst of the arguments</param>
    /// <returns>True if the method parameters match the provided arguments, otherwise false.</returns>
    private bool SameParametes(MethodInfo method, object?[] args) {
        var methodParams = method.GetParameters();
        if (methodParams.Length != args.Length) return false;

        for (int i = 0; i < methodParams.Length; ++i)
            if (!methodParams[i].ParameterType.Equals(args[i].GetType())) return false;

        return true;
    }

    public override bool TryInvokeMember(InvokeMemberBinder binder, object?[] args, out object result) {
        if (DynamicMethods.TryGetValue(binder.Name, out var methods)) {
            Logger.debug($"Invoking method '{binder.Name}'");

            foreach (var method in methods) {
                if (!SameParametes(method.Method, args)) continue;

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

    /// <summary>
    /// Applies a plugin component to the current context.
    /// </summary>
    /// <param name="component">The instance of the component</param>
    public void ApplyComponent(PluginComponent component) {
        Logger.debug($"Applying component '{component}'.");

        if (PluginComponents.ContainsKey(component.GetType())) {
            Logger.warn($"Component '{component}' already applied.");
            return;
        }
        PluginComponents.Add(component.GetType(), component);

        foreach (var e in component.plugin.enums) {
            ManilaEngine.GetInstance().CurrentContext.ApplyEnum(e);
        }

        ApplyPlugin(component.plugin);

        foreach (var prop in component.GetType().GetProperties()) {
            AddScriptProperty(prop, component);
        }
    }

    /// <summary>
    /// Applies a plugin to the current context.
    /// </summary>
    /// <param name="plugin">The instance of the plugin</param>
    public void ApplyPlugin(ManilaPlugin plugin) {
        if (plugins.Contains(plugin.GetType())) {
            Logger.warn($"Plugin '{plugin}' already applied.");
            return;
        }

        plugins.Add(plugin.GetType());

        ManilaEngine.GetInstance().CurrentContext.ScriptEngine.AddHostType(plugin.GetType().Name, plugin.GetType());
    }

    /// <summary>
    /// Check if the component is applied to the current context.
    /// </summary>
    /// <typeparam name="T">The component type</typeparam>
    /// <returns>True if the component is applied, otherwise false.</returns>
    public bool HasComponent<T>() where T : PluginComponent {
        return PluginComponents.ContainsKey(typeof(T));
    }
    /// <summary>
    /// Get the component values from the current context.
    /// </summary>
    /// <typeparam name="T">The component type</typeparam>
    /// <returns>The values of the component for the current context</returns>
    /// <exception cref="Exception">The component was not found on the context</exception>
    public T getComponent<T>() where T : PluginComponent {
        if (!PluginComponents.ContainsKey(typeof(T))) throw new Exception($"Component '{typeof(T)}' not found.");
        return (T) PluginComponents[typeof(T)];
    }
}
