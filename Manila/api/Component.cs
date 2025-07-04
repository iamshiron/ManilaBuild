using Microsoft.ClearScript;
using System.Dynamic;
using System.Reflection;
using Shiron.Manila.Ext;
using Shiron.Manila.Utils;
using Shiron.Manila.Attributes;
using Shiron.Manila.Logging;

namespace Shiron.Manila.API;

/// <summary>
/// Represents a component in the build script. Components are used to group tasks and plugins. Can either be a workspace or a project.
/// </summary>
public class Component(string path) : DynamicObject, IScriptableObject {
    [ScriptProperty(true)]
    public DirHandle Path { get; private set; } = new DirHandle(path);

    public Dictionary<Type, PluginComponent> PluginComponents { get; } = [];
    public List<Type> plugins { get; } = [];
    public Dictionary<string, List<Delegate>> DynamicMethods { get; } = [];
    public List<Task> Tasks { get; } = [];
    public List<Type> DependencyTypes { get; } = [];

    /// <summary>
    /// The GetIdentifier method returns a string that uniquely identifies the component.
    /// </summary>
    /// <returns>The identifier</returns>
    public virtual string GetIdentifier() {
        string relativeDir = System.IO.Path.GetRelativePath(ManilaEngine.GetInstance().RootDir, Path.Handle);
        return relativeDir.Replace(System.IO.Path.DirectorySeparatorChar, ':').ToLower();
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

    public override bool TryInvokeMember(InvokeMemberBinder binder, object?[]? args, out object? result) {
        args ??= [];
        result = null;

        if (DynamicMethods.TryGetValue(binder.Name, out var methods)) {
            foreach (var method in methods) {
                if (!FunctionUtils.SameParametes(method.Method, args)) continue;

                var methodParams = method.Method.GetParameters();
                for (int i = 0; i < methodParams.Length; ++i) {
                    var param = methodParams[i];
                    Logger.Debug($"Parameter: {param.Name}");

                    // Convert enum strings to enum values
                    if (param.ParameterType.IsEnum) {
                        var type = param.ParameterType;
                        if (args[i] != null) {
                            args[i] = Enum.Parse(type, args[i]!.ToString()!);
                        } else {
                            throw new ArgumentNullException($"Argument at position {i} for enum parameter '{param.Name}' cannot be null.");
                        }
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
            Logger.Warning($"Component '{component}' already applied.");
            return;
        }
        PluginComponents.Add(component.GetType(), component);

        if (component.plugin != null) {
            foreach (var e in component.plugin.Enums) {
                var currentContext = ManilaEngine.GetInstance().CurrentContext;
                if (currentContext != null) {
                    currentContext.ApplyEnum(e);
                } else {
                    Logger.Warning("CurrentContext is null. Cannot apply enum.");
                }
            }

            ApplyPlugin(component.plugin);
        }

        foreach (var prop in component.GetType().GetProperties()) {
            if (prop.GetCustomAttribute<ScriptProperty>() == null) continue;
            AddScriptProperty(prop, component);
        }
    }

    /// <summary>
    /// Applies a plugin to the current context.
    /// </summary>
    /// <param name="plugin">The instance of the plugin</param>
    public void ApplyPlugin(ManilaPlugin plugin) {
        var engineInstance = ManilaEngine.GetInstance();
        var currentContext = engineInstance.CurrentContext;
        if (currentContext == null) {
            Logger.Warning("CurrentContext is null. Cannot apply plugin.");
            return;
        }
        var scriptEngine = currentContext.ScriptEngine;
        if (scriptEngine == null) {
            Logger.Warning("ScriptEngine is null. Cannot add host type.");
            return;
        }

        scriptEngine.AddHostType(plugin.GetType().Name, plugin.GetType());

        if (plugin.Dependencies != null) {
            foreach (var t in plugin.Dependencies) {
                if (t == null) continue;
                DependencyTypes.Add(t);

                if (currentContext.ManilaAPI == null) {
                    Logger.Warning("ManilaAPI is null. Cannot add dependency function.");
                    continue;
                }

                currentContext.ManilaAPI.AddFunction(
                    (Activator.CreateInstance(t) as Dependency)?.Type!,
                    delegate (dynamic[] args) {
                        var dep = Activator.CreateInstance(t) as Dependency;
                        if (dep == null) {
                            Logger.Warning($"Could not create instance of dependency type '{t}'.");
                            return null;
                        }
                        dep.Create((object[]) args);
                        return dep;
                    }
                );
            }
        }

        if (plugins.Contains(plugin.GetType())) {
            Logger.Warning($"Plugin '{plugin}' already applied.");
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
        if (PluginComponents.TryGetValue(typeof(T), out var component))
            return (T) component;

        foreach (var p in PluginComponents) {
            if (typeof(T).IsAssignableFrom(p.Key))
                return (T) p.Value;
        }

        throw new Exception($"Component of type {typeof(T).Name} not found in this context.");
    }

    public LanguageComponent GetLanguageComponent() {
        foreach (var component in PluginComponents.Values) {
            if (component is LanguageComponent languageComponent) return languageComponent;
        }
        throw new Exception("No language component found.");
    }
}
