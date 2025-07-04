using System.Dynamic;
using System.Reflection;
using Microsoft.ClearScript;
using Shiron.Manila.Attributes;
using Shiron.Manila.Logging;

namespace Shiron.Manila.Utils;

/// <summary>
/// Represents a dynamic object that can be exposed to the script context.
/// This class is used to expose methods and properties to the script context.
/// </summary>
public abstract class ExposedDynamicObject : DynamicObject, IScriptableObject {
    /// <summary>
    /// Holds all functions that are exposed to the script context.
    /// The key is the name of the function as it will be exposed to the script context.
    /// The value is a list of delegates that represent the functions, as there can be multiple functions with the same name but different signatures.
    /// </summary>
    private readonly Dictionary<string, List<Delegate>> _functions = [];

    /// <summary>
    /// Adds a function to the list of functions that are exposed to the script context.
    /// The function will be exposed with the name of the method as it will be exposed to the script context.
    /// </summary>
    /// <param name="exposedName">The exposed named</param>
    /// <param name="info">The method info</param>
    /// <param name="target">The target, null for a static function</param>
    public void AddFunction(string exposedName, MethodInfo info, object? target = null) {
        if (!_functions.ContainsKey(exposedName)) _functions[exposedName] = [];
        _functions[exposedName].Add(FunctionUtils.ToDelegate(target, info));
    }
    public void AddFunction(string exposedName, Delegate func, object? target = null) {
        if (!_functions.ContainsKey(exposedName)) _functions[exposedName] = [];
        _functions[exposedName].Add(func);
    }
    /// <summary>
    /// Adds a property to the list of properties that are exposed to the script context.
    /// The property will be exposed JS styled naming convention (first letter lowercase).
    /// </summary>
    /// <param name="info">The property</param>
    /// <param name="target">The target</param>
    /// <exception cref="Exception">Property is not a ScriptProperty</exception>
    public void AddProperty(PropertyInfo info, object? target = null) {
        var attribute = info.GetCustomAttribute<ScriptProperty>();
        if (attribute == null) throw new Exception($"Property '{info.Name}' is not a script property.");

        AddProperty(
            attribute.ExposedName ?? info.Name[0].ToString().ToLower() + info.Name[1..],
            attribute.GetterName ?? "get" + info.Name,
            attribute.SetterName ?? "set" + info.Name,
            info,
            attribute.Immutable,
            target
        );
    }
    /// <summary>
    /// Adds a property to the list of properties that are exposed to the script context.
    /// The property will be exposed with the name of the property as it will be exposed to the script context.
    /// <param name="propertyName">The exposed name</param>
    /// <param name="getterName">The getter name</param>
    /// <param name="setterName">The setter name</param>
    /// <param name="info">The property info</param>
    /// <param name="immutable">If the property is immutable</param>
    /// <param name="target">The target, null for a static function</param>
    /// <exception cref="Exception">If the property is not settable but is marked as mutable.</exception>
    /// </summary>
    public void AddProperty(string propertyName, string getterName, string setterName, PropertyInfo info, bool immutable, object? target) {
        if (!immutable) {
            if (info.SetMethod == null) throw new Exception($"Property '{info.Name}' is not settable but is marked as mutable.");
            AddFunction(setterName, info.SetMethod, target);
        }

        if (info.GetMethod != null) {
            AddFunction(getterName, info.GetMethod, target);
        }
    }

    public virtual void OnExposedToScriptCode(ScriptEngine engine) {
        foreach (var m in this.GetType().GetMethods()) {
            if (m.GetCustomAttribute<ScriptFunction>() == null) continue;
            AddFunction(m.Name[0].ToString().ToLower() + m.Name[1..], m, m.IsStatic ? null : this);
        }
        foreach (var prop in this.GetType().GetProperties()) {
            if (prop.GetCustomAttribute<ScriptProperty>() == null) continue;
            AddProperty(prop, this);
        }
    }

    override public bool TryInvokeMember(InvokeMemberBinder binder, object?[]? args, out object? result) {
        args ??= [];

        if (_functions.TryGetValue(binder.Name, out var methods)) {
            foreach (var method in methods) {
                if (!FunctionUtils.SameParametes(method.Method, args)) continue;

                var methodParams = method.Method.GetParameters();
                for (int i = 0; i < methodParams.Length; ++i) {
                    var param = methodParams[i];
                    Logger.Debug($"Parameter: {param.Name}");

                    // Convert enum strings to enum values
                    if (param.ParameterType.IsEnum) {
                        var type = param.ParameterType;
                        var argValue = args[i]?.ToString();
                        if (argValue == null)
                            throw new ArgumentNullException(param.Name, $"Argument for enum parameter '{param.Name}' cannot be null.");
                        args[i] = Enum.Parse(type, argValue);
                    }
                }

                result = method.DynamicInvoke(args);
                return true;
            }

            // Check for a method that takes a single object parameter and call it with an object array
            if (methods.Count == 1 && methods[0].Method.GetParameters().Length == 1) {
                var method = methods[0];
                var methodParams = method.Method.GetParameters();
                if (methodParams.Length == 1 && methodParams[0].ParameterType.IsArray) {
                    result = method.DynamicInvoke([args]);
                    return true;
                }
            }
        }

        Logger.Debug($"Method '{binder.Name}' not found.");
        return base.TryInvokeMember(binder, args, out result);
    }

    public override IEnumerable<string> GetDynamicMemberNames() => _functions.Keys.ToArray();
}
