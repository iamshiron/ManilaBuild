
using System.Dynamic;
using System.Reflection;
using Microsoft.ClearScript;
using Shiron.Manila.Attributes;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Ext;
using Shiron.Manila.Logging;
using Shiron.Manila.Utils;

namespace Shiron.Manila.API.Bridges;

public abstract class ScriptBridge : DynamicObject, IScriptableObject {
    /// <summary>
    /// Dynamic methods available for script invocation.
    /// </summary>
    public Dictionary<string, List<Delegate>> DynamicMethods { get; } = [];

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
                    if (param.ParameterType.IsEnum) {
                        var type = param.ParameterType;
                        if (args[i] != null) {
                            args[i] = Enum.Parse(type, args[i]!.ToString()!);
                        } else {
                            throw new ManilaException($"Argument at position {i} for enum parameter '{param.Name}' cannot be null.");
                        }
                    }
                }


                result = method.DynamicInvoke(args);
                return true;
            }
        }

        return base.TryInvokeMember(binder, args, out result);
    }
}
