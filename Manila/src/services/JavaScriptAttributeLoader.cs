
using System.Reflection;
using Microsoft.ClearScript;

namespace Shiron.Manila.Services;

public class JavaScriptAttributeLoader : CustomAttributeLoader {
    public override T[]? LoadCustomAttributes<T>(ICustomAttributeProvider resource, bool inherit) {
        var declaredAttributes = base.LoadCustomAttributes<T>(resource, inherit);
        if (declaredAttributes.Length == 0 && typeof(T) == typeof(ScriptMemberAttribute) && resource is MemberInfo member) {
            var lowerCamelCaseName = char.ToLowerInvariant(member.Name[0]) + member.Name[1..];
            return new[] { new ScriptMemberAttribute(lowerCamelCaseName) } as T[];
        }
        return declaredAttributes;
    }
}
