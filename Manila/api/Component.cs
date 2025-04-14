using Microsoft.ClearScript;
using System.Dynamic;
using System.Reflection;
using Shiron.Manila.Ext;
using Shiron.Manila.Utils;
using Shiron.Manila.Attributes;

namespace Shiron.Manila.API;

/// <summary>
/// Represents a component in the build script. Components are used to group tasks and plugins. Can either be a workspace or a project.
/// </summary>
public class Component(string path) : DynamicObject, IScriptableObject {
    [ScriptProperty(true)]
    public Dir Path { get; private set; } = new Dir(path);

    public Dictionary<Type, PluginComponent> PluginComponents { get; } = [];
    public List<Type> plugins { get; } = [];
    public Dictionary<string, List<Delegate>> DynamicMethods { get; } = [];
    public List<Task> tasks { get; } = [];
    public List<Type> dependencyTypes { get; } = [];

    /// <summary>
    /// The GetIdentifier method returns a string that uniquely identifies the component.
    /// </summary>
    /// <returns>The identifier</returns>
    public virtual string GetIdentifier() {
        string relativeDir = System.IO.Path.GetRelativePath(ManilaEngine.GetInstance().Root, Path.path);
        return ":" + relativeDir.Replace(System.IO.Path.DirectorySeparatorChar, ':').ToLower();
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

        Logger.Debug($"Adding property {propertyName}...");
        Logger.Debug($"Script property name: {scriptPropertyName}");
        Logger.Debug($"Script property setter name: {scriptPropertySetterName}");
        Logger.Debug($"Script property getter name: {scriptPropertyGetterName}");

        var scriptPropertyInfo = prop.GetCustomAttribute<ScriptProperty>();
        if (scriptPropertyInfo == null) throw new Exception($"Property '{prop.Name}' is not a script property.");

        var setMethod = prop.GetSetMethod();

        if (setMethod != null && !scriptPropertyInfo.Immutable) {
            ManilaEngine.GetInstance().CurrentContext!.ScriptEngine.AddHostObject(scriptPropertyName, FunctionUtils.ToDelegate(obj, prop.GetSetMethod()!));
        }


        var setterMethods = DynamicMethods.ContainsKey(scriptPropertySetterName) ? DynamicMethods[scriptPropertySetterName] : [];
        var getterMethods = DynamicMethods.ContainsKey(scriptPropertyGetterName) ? DynamicMethods[scriptPropertyGetterName] : [];

        if (setMethod != null && !scriptPropertyInfo.Immutable) setterMethods.Add(FunctionUtils.ToDelegate(obj, setMethod));
        getterMethods.Add(FunctionUtils.ToDelegate(obj, prop.GetGetMethod()!));

        DynamicMethods[scriptPropertySetterName] = setterMethods;
        DynamicMethods[scriptPropertyGetterName] = getterMethods;
    }
    public void AddScriptFunction(MethodInfo prop, ScriptEngine engine, object? obj = null) {
        if (obj == null) obj = this;

        Logger.Debug($"Adding function '{prop.Name}' to script context.");
        var scriptFunctionInfo = prop.GetCustomAttribute<ScriptFunction>();
        if (scriptFunctionInfo == null) throw new Exception($"Method '{prop.Name}' is not a script function.");

        var methods = DynamicMethods.ContainsKey(prop.Name) ? DynamicMethods[prop.Name] : new List<Delegate>();
        methods.Add(FunctionUtils.ToDelegate(obj, prop));
        DynamicMethods[prop.Name] = methods;

        engine.AddHostObject(prop.Name, FunctionUtils.ToDelegate(obj, prop));

        Logger.Debug($"Added function '{prop.Name}' to script context.");
    }

    public override bool TryInvokeMember(InvokeMemberBinder binder, object?[] args, out object result) {
        if (DynamicMethods.TryGetValue(binder.Name, out var methods)) {
            Logger.Debug($"Invoking method '{binder.Name}'");

            foreach (var method in methods) {
                if (!FunctionUtils.SameParametes(method.Method, args)) continue;

                var methodParams = method.Method.GetParameters();
                for (int i = 0; i < methodParams.Length; ++i) {
                    var param = methodParams[i];
                    Logger.Debug($"Parameter: {param.Name}");

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

        Logger.Debug($"Method '{binder.Name}' not found.");
        return base.TryInvokeMember(binder, args, out result);
    }

    /// <summary>
    /// Applies a plugin component to the current context.
    /// </summary>
    /// <param name="component">The instance of the component</param>
    public void ApplyComponent(PluginComponent component) {
        Logger.Debug($"Applying component '{component}'.");

        if (PluginComponents.ContainsKey(component.GetType())) {
            Logger.Warn($"Component '{component}' already applied.");
            return;
        }
        PluginComponents.Add(component.GetType(), component);

        foreach (var e in component.plugin.Enums) {
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
        ManilaEngine.GetInstance().CurrentContext.ScriptEngine.AddHostType(plugin.GetType().Name, plugin.GetType());
        foreach (var t in plugin.Dependencies) {
            dependencyTypes.Add(t);
            ManilaEngine.GetInstance().CurrentContext.ManilaAPI.AddFunction((Activator.CreateInstance(t) as Dependency).Type, delegate (dynamic[] args) {
                var dep = Activator.CreateInstance(t) as Dependency;
                dep.Create((object[]) args);
                return dep;
            });
        }

        if (plugins.Contains(plugin.GetType())) {
            Logger.Warn($"Plugin '{plugin}' already applied.");
            return;
        }

        plugins.Add(plugin.GetType());
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
    public T GetComponent<T>() where T : PluginComponent {
        if (!PluginComponents.ContainsKey(typeof(T))) throw new Exception($"Component '{typeof(T)}' not found.");
        return (T) PluginComponents[typeof(T)];
    }

    public LanguageComponent GetLanguageComponent() {
        foreach (var component in PluginComponents.Values) {
            if (component is LanguageComponent languageComponent) return languageComponent;
        }
        throw new Exception("No language component found.");
    }
}
