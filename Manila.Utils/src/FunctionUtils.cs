using System.Linq.Expressions;
using System.Reflection;

namespace Shiron.Manila.Utils;

public static class FunctionUtils {
    /// <summary>
    /// Converts a method to a delegate.
    /// </summary>
    /// <param name="o">The instance object on which the method is invoked, or null for static methods.</param>
    /// <param name="method">The MethodInfo representing the method to convert.</param>
    /// <returns>A delegate that can be used to invoke the method.</returns>
    /// <exception cref="ArgumentException">Thrown when delegate creation fails.</exception>
    /// <exception cref="NotSupportedException">Thrown when method has more than 4 parameters.</exception>
    /// <remarks>
    /// This method creates a delegate for the specified method. It supports both instance and static methods,
    /// and methods with up to 4 parameters. For methods that cannot be directly converted using CreateDelegate,
    /// it falls back to using expression trees.
    /// </remarks>
    public static Delegate ToDelegate(object? o, MethodInfo method) {
        try {
            if (method.ReturnType == typeof(void)) {
                var paramTypes = method.GetParameters().Select(p => p.ParameterType).ToArray();
                Type delegateType = paramTypes.Length switch {
                    0 => typeof(Action),
                    1 => typeof(Action<>).MakeGenericType(paramTypes),
                    2 => typeof(Action<,>).MakeGenericType(paramTypes),
                    3 => typeof(Action<,,>).MakeGenericType(paramTypes),
                    4 => typeof(Action<,,,>).MakeGenericType(paramTypes),
                    _ => throw new ArgumentException($"Methods with {paramTypes.Length} parameters are not supported")
                };
                return Delegate.CreateDelegate(delegateType, o, method, throwOnBindFailure: false)
                       ?? CreateDelegateWithExpression(o, method, delegateType);
            } else {
                var paramTypes = method.GetParameters().Select(p => p.ParameterType).ToArray();
                Type[] typeArgs = paramTypes.Append(method.ReturnType).ToArray();
                Type delegateType = paramTypes.Length switch {
                    0 => typeof(Func<>).MakeGenericType(method.ReturnType),
                    1 => typeof(Func<,>).MakeGenericType(typeArgs),
                    2 => typeof(Func<,,>).MakeGenericType(typeArgs),
                    3 => typeof(Func<,,,>).MakeGenericType(typeArgs),
                    4 => typeof(Func<,,,,>).MakeGenericType(typeArgs),
                    _ => throw new ArgumentException($"Methods with {paramTypes.Length} parameters are not supported")
                };
                return Delegate.CreateDelegate(delegateType, o, method, throwOnBindFailure: false)
                       ?? CreateDelegateWithExpression(o, method, delegateType);
            }
        } catch (Exception ex) {
            throw new ArgumentException(
                $"Failed to create delegate for method {method.Name} on type {method.DeclaringType?.Name}. " +
                $"Return type: {method.ReturnType.Name}, " +
                $"Parameter types: {string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name))}. " +
                $"Error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Creates a delegate using an expression tree.
    /// </summary>
    /// <param name="target">The target object instance, or null for static methods.</param>
    /// <param name="method">The method to create a delegate for.</param>
    /// <param name="delegateType">The type of delegate to create.</param>
    /// <returns>A delegate of the specified type that invokes the method.</returns>
    /// <remarks>
    /// This method uses expression trees to create a delegate when the standard Delegate.CreateDelegate
    /// method fails. It supports both instance and static methods with up to 4 parameters.
    /// </remarks>
    private static Delegate CreateDelegateWithExpression(object? target, MethodInfo method, Type delegateType) {
        // This is a fallback approach using Expression trees when standard delegate creation fails
        ParameterExpression[] parameters = [.. method.GetParameters().Select(p => Expression.Parameter(p.ParameterType, p.Name))];

        Expression? instance = target != null ? Expression.Constant(target) : null;
        MethodCallExpression call = instance != null
            ? Expression.Call(instance, method, parameters)
            : Expression.Call(method, parameters);

        LambdaExpression lambda = method.ReturnType == typeof(void)
            ? Expression.Lambda(delegateType, call, parameters)
            : Expression.Lambda(delegateType, call, parameters);

        return lambda.Compile();
    }

    /// <summary>
    /// Check if the method has the same parameters as the arguments.
    /// </summary>
    /// <param name="method">The method to check</param>
    /// <param name="args">The list of the arguments</param>
    /// <returns>True if the method parameters match the provided arguments, otherwise false.</returns>
    public static bool SameParameters(MethodInfo method, object?[] args) {
        var methodParams = method.GetParameters();
        if (methodParams.Length != args.Length) return false;

        for (int i = 0; i < methodParams.Length; ++i)
            if (!methodParams[i].ParameterType.Equals(args[i]!.GetType())) return false;

        return true;
    }
}
