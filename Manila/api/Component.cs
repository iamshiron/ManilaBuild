using System.Dynamic;
using System.Reflection;
using Microsoft.ClearScript;
using Newtonsoft.Json;
using Shiron.Manila.Attributes;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Ext;
using Shiron.Manila.Logging;
using Shiron.Manila.Utils;

namespace Shiron.Manila.API;

public static class ComponentContextApplyer {
    /// <summary>
    /// Adds a property that gets exposed to the script context.
    /// </summary>
    /// <param name="prop">The property to add.</param>
    /// <param name="obj">The instance of the class.</param>
    /// <exception cref="Exception">Thrown when property lacks ScriptProperty attribute.</exception>
    public static void AddScriptProperty(ILogger logger, IScriptContext context, Component applyTo, PropertyInfo prop, object? obj = null) {
        obj ??= applyTo;

        var propertyName = prop.Name;
        var scriptPropertyName = propertyName[0].ToString().ToLower() + propertyName[1..]; // Convert first letter to lowercase for JS naming convention
        var scriptPropertyGetterName = "get" + propertyName;
        var scriptPropertySetterName = "set" + propertyName;

        var scriptPropertyInfo = prop.GetCustomAttribute<ScriptProperty>() ?? throw new Exception($"Property '{prop.Name}' is not a script property.");
        var setMethod = prop.GetSetMethod();

        if (setMethod != null && !scriptPropertyInfo.Immutable) {
            context.ScriptEngine.AddHostObject(scriptPropertyName, FunctionUtils.ToDelegate(obj, prop.GetSetMethod()!));
        }


        var setterMethods = applyTo.DynamicMethods.TryGetValue(scriptPropertySetterName, out var setterValue) ? setterValue : [];
        var getterMethods = applyTo.DynamicMethods.TryGetValue(scriptPropertyGetterName, out var getterValue) ? getterValue : [];

        if (setMethod != null && !scriptPropertyInfo.Immutable) setterMethods.Add(FunctionUtils.ToDelegate(obj, setMethod));
        getterMethods.Add(FunctionUtils.ToDelegate(obj, prop.GetGetMethod()!));

        applyTo.DynamicMethods[scriptPropertySetterName] = setterMethods;
        applyTo.DynamicMethods[scriptPropertyGetterName] = getterMethods;
    }

    /// <summary>
    /// Adds a script function to the component's dynamic methods.
    /// </summary>
    /// <param name="prop">The method to add.</param>
    /// <param name="engine">The script engine.</param>
    /// <param name="obj">The instance of the class.</param>
    /// <exception cref="Exception">Thrown when method lacks ScriptFunction attribute.</exception>
    public static void AddScriptFunction(ILogger logger, Component applyTo, MethodInfo prop, ScriptEngine engine, object? obj = null) {
        obj ??= applyTo;

        logger.Debug($"Adding function '{prop.Name}' to script context.");
        _ = prop.GetCustomAttribute<ScriptFunction>() ?? throw new Exception($"Method '{prop.Name}' is not a script function.");
        var methods = applyTo.DynamicMethods.TryGetValue(prop.Name, out List<Delegate>? value) ? value : [];
        methods.Add(FunctionUtils.ToDelegate(obj, prop));
        applyTo.DynamicMethods[prop.Name] = methods;

        engine.AddHostObject(prop.Name, FunctionUtils.ToDelegate(obj, prop));

        logger.Debug($"Added function '{prop.Name}' to script context.");
    }

    /// <summary>
    /// Applies a plugin to this component.
    /// </summary>
    /// <param name="plugin">The plugin to apply.</param>
    public static void ApplyPlugin(ILogger logger, ScriptContext context, Component applyTo, ManilaPlugin plugin) {
        if (context.ManilaAPI == null) throw new ManilaException("ManilaAPI is not initialized in the script context.");

        context.ScriptEngine.AddHostType(plugin.GetType().Name, plugin.GetType());

        if (plugin.Dependencies != null) {
            foreach (var t in plugin.Dependencies) {
                if (t == null) continue;
                applyTo.DependencyTypes.Add(t);

                context.ManilaAPI.AddFunction(
                    (Activator.CreateInstance(t) as Dependency)?.Type!,
                    delegate (dynamic[] args) {
                        if (Activator.CreateInstance(t) is not Dependency dep) {
                            logger.Warning($"Could not create instance of dependency type '{t}'.");
                            return null;
                        }
                        dep.Create((object[]) args);
                        return dep;
                    }
                );
            }
        }

        if (applyTo.Plugins.Contains(plugin.GetType())) {
            logger.Warning($"Plugin '{plugin}' already applied.");
            return;
        }

        applyTo.Plugins.Add(plugin.GetType());
    }

    /// <summary>
    /// Applies a plugin component to this component.
    /// </summary>
    /// <param name="component">The plugin component to apply.</param>
    public static void ApplyComponent(ILogger logger, ScriptContext context, Component applyTo, PluginComponent component) {
        logger.Debug($"Applying component '{component}'.");

        if (applyTo.PluginComponents.ContainsKey(component.GetType())) {
            logger.Warning($"Component '{component}' already applied.");
            return;
        }
        applyTo.PluginComponents.Add(component.GetType(), component);

        if (component._plugin != null) {
            foreach (var e in component._plugin.Enums) {
                context.ApplyEnum(e);
            }

            ApplyPlugin(logger, context, applyTo, component._plugin);
        }

        foreach (var prop in component.GetType().GetProperties()) {
            if (prop.GetCustomAttribute<ScriptProperty>() == null) continue;
            AddScriptProperty(logger, context, applyTo, prop, component);
        }
    }
}

/// <summary>
/// Represents a component in the build script that groups jobs and plugins.
/// </summary>
public class Component(ILogger logger, string rootDir, string path) : DynamicObject, IScriptableObject {
    public readonly string RootDir = rootDir;
    private readonly ILogger _logger = logger;

    /// <summary>
    /// The directory path of this component.
    /// </summary>
    [ScriptProperty(true)]
    public DirHandle Path { get; private set; } = new DirHandle(path);

    /// <summary>
    /// Collection of plugin components applied to this component.
    /// </summary>
    public Dictionary<Type, PluginComponent> PluginComponents { get; } = [];

    /// <summary>
    /// List of plugin types applied to this component.
    /// </summary>
    public List<Type> Plugins { get; } = [];

    /// <summary>
    /// Dynamic methods available for script invocation.
    /// </summary>
    public Dictionary<string, List<Delegate>> DynamicMethods { get; } = [];

    /// <summary>
    /// Collection of jobs belonging to this component.
    /// </summary>
    public List<Job> Jobs { get; } = [];

    /// <summary>
    /// Types of dependencies used by this component.
    /// </summary>
    public List<Type> DependencyTypes { get; } = [];

    /// <summary>
    /// Returns a unique identifier for this component.
    /// </summary>
    /// <returns>The component identifier.</returns>
    public virtual string GetIdentifier() {
        string relativeDir = System.IO.Path.GetRelativePath(RootDir, Path.Handle);
        return relativeDir.Replace(System.IO.Path.DirectorySeparatorChar, ':').ToLower();
    }

    /// <summary>
    /// Called when the component is exposed to script code.
    /// </summary>
    /// <param name="engine">The script engine.</param>
    public void OnExposedToScriptCode(ScriptEngine engine) {
    }

    /// <summary>
    /// Gets the names of all dynamic members available for invocation.
    /// </summary>
    /// <returns>Collection of dynamic member names.</returns>
    public override IEnumerable<string> GetDynamicMemberNames() {
        return DynamicMethods.Keys;
    }

    /// <summary>
    /// Attempts to invoke a dynamic member method with the given arguments.
    /// </summary>
    /// <param name="binder">The invoke member binder.</param>
    /// <param name="args">The method arguments.</param>
    /// <param name="result">The result of the method invocation.</param>
    /// <returns>True if the method was found and invoked successfully.</returns>
    public override bool TryInvokeMember(InvokeMemberBinder binder, object?[]? args, out object? result) {
        args ??= [];
        result = null;

        if (DynamicMethods.TryGetValue(binder.Name, out var methods)) {
            foreach (var method in methods) {
                if (!FunctionUtils.SameParametes(method.Method, args)) continue;

                var methodParams = method.Method.GetParameters();
                for (int i = 0; i < methodParams.Length; ++i) {
                    var param = methodParams[i];
                    _logger.Debug($"Parameter: {param.Name}");

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

        _logger.Debug($"Method '{binder.Name}' not found.");
        return base.TryInvokeMember(binder, args, out result);
    }

    /// <summary>
    /// Checks if a component type is applied to this component.
    /// </summary>
    /// <typeparam name="T">The component type to check.</typeparam>
    /// <returns>True if the component is applied.</returns>
    public bool HasComponent<T>() where T : PluginComponent {
        return PluginComponents.ContainsKey(typeof(T));
    }
    /// <summary>
    /// Gets a component instance of the specified type.
    /// </summary>
    /// <typeparam name="T">The component type to retrieve.</typeparam>
    /// <returns>The component instance.</returns>
    /// <exception cref="Exception">Thrown when the component is not found.</exception>
    public T GetComponent<T>() where T : PluginComponent {
        if (PluginComponents.TryGetValue(typeof(T), out var component))
            return (T) component;

        foreach (var p in PluginComponents) {
            if (typeof(T).IsAssignableFrom(p.Key))
                return (T) p.Value;
        }

        throw new Exception($"Component of type {typeof(T).Name} not found in this context.");
    }

    /// <summary>
    /// Gets the language component applied to this component.
    /// </summary>
    /// <returns>The language component instance.</returns>
    /// <exception cref="Exception">Thrown when no language component is found.</exception>
    public LanguageComponent GetLanguageComponent() {
        foreach (var component in PluginComponents.Values) {
            if (component is LanguageComponent languageComponent) return languageComponent;
        }
        throw new Exception("No language component found.");
    }

    /// <summary>
    /// Finalizes the component by building all jobs.
    /// </summary>
    /// <param name="manilaAPI">The Manila API instance.</param>
    public virtual void Finalize(Manila manilaAPI) {
        Jobs.AddRange(manilaAPI.JobBuilders.Select(b => b.Build()));
    }
}
