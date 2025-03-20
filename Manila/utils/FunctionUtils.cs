using System.Linq.Expressions;
using System.Reflection;

namespace Shiron.Manila.Utils;

public static class FunctionUtils {
	public static Delegate toDelegate(object o, MethodInfo method) {
		try {
			if (method.ReturnType == typeof(void)) {
				var paramTypes = method.GetParameters().Select(p => p.ParameterType).ToArray();
				Type delegateType = paramTypes.Length switch {
					0 => typeof(Action),
					1 => typeof(Action<>).MakeGenericType(paramTypes),
					2 => typeof(Action<,>).MakeGenericType(paramTypes),
					3 => typeof(Action<,,>).MakeGenericType(paramTypes),
					4 => typeof(Action<,,,>).MakeGenericType(paramTypes),
					_ => throw new NotSupportedException($"Methods with {paramTypes.Length} parameters are not supported")
				};
				return Delegate.CreateDelegate(delegateType, o, method, throwOnBindFailure: false)
					   ?? createDelegateWithExpression(o, method, delegateType);
			} else {
				var paramTypes = method.GetParameters().Select(p => p.ParameterType).ToArray();
				Type[] typeArgs = paramTypes.Append(method.ReturnType).ToArray();
				Type delegateType = paramTypes.Length switch {
					0 => typeof(Func<>).MakeGenericType(method.ReturnType),
					1 => typeof(Func<,>).MakeGenericType(typeArgs),
					2 => typeof(Func<,,>).MakeGenericType(typeArgs),
					3 => typeof(Func<,,,>).MakeGenericType(typeArgs),
					4 => typeof(Func<,,,,>).MakeGenericType(typeArgs),
					_ => throw new NotSupportedException($"Methods with {paramTypes.Length} parameters are not supported")
				};
				return Delegate.CreateDelegate(delegateType, o, method, throwOnBindFailure: false)
					   ?? createDelegateWithExpression(o, method, delegateType);
			}
		} catch (Exception ex) {
			throw new ArgumentException(
				$"Failed to create delegate for method {method.Name} on type {method.DeclaringType?.Name}. " +
				$"Return type: {method.ReturnType.Name}, " +
				$"Parameter types: {string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name))}. " +
				$"Error: {ex.Message}", ex);
		}
	}

	private static Delegate createDelegateWithExpression(object target, MethodInfo method, Type delegateType) {
		// This is a fallback approach using Expression trees when standard delegate creation fails
		ParameterExpression[] parameters = method.GetParameters()
			.Select(p => Expression.Parameter(p.ParameterType, p.Name))
			.ToArray();

		Expression instance = target != null ? Expression.Constant(target) : null;
		MethodCallExpression call = instance != null
			? Expression.Call(instance, method, parameters)
			: Expression.Call(method, parameters);

		LambdaExpression lambda = method.ReturnType == typeof(void)
			? Expression.Lambda(delegateType, call, parameters)
			: Expression.Lambda(delegateType, call, parameters);

		return lambda.Compile();
	}
}
