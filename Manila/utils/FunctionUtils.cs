using System.Linq.Expressions;
using System.Reflection;

namespace Shiron.Manila.Utils;

public static class FunctionUtils {
	public static Delegate toDelegate(object o, MethodInfo method) {
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
			return Delegate.CreateDelegate(delegateType, o, method);
		} else {
			var paramTypes = method.GetParameters().Select(p => p.ParameterType).ToArray();
			Type delegateType = paramTypes.Length switch {
				0 => typeof(Func<>).MakeGenericType(method.ReturnType),
				1 => typeof(Func<,>).MakeGenericType(paramTypes.Append(method.ReturnType).ToArray()),
				2 => typeof(Func<,,>).MakeGenericType(paramTypes.Append(method.ReturnType).ToArray()),
				3 => typeof(Func<,,,>).MakeGenericType(paramTypes.Append(method.ReturnType).ToArray()),
				4 => typeof(Func<,,,,>).MakeGenericType(paramTypes.Append(method.ReturnType).ToArray()),
				_ => throw new NotSupportedException($"Methods with {paramTypes.Length} parameters are not supported")
			};
			return Delegate.CreateDelegate(delegateType, o, method);
		}
	}
}
