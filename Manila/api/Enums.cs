using Shiron.Manila.Attributes;

namespace Shiron.Manila.API;

/// <summary>
/// A enum class containing the different platforms that Manila supports. Used to be exposed to the scripting environment.
/// </summary>
/// <param name="name">The stringified name of the platform</param>
[ScriptEnum]
public class EPlatform(string name) {
    public readonly string name = name;
    public static readonly EPlatform Windows = new("windows");
    public static readonly EPlatform Linux = new("linux");

    public static implicit operator string(EPlatform p) {
        return p.name;
    }

    public override bool Equals(object? obj) {
        return obj is EPlatform platform && name == platform.name;
    }
    public override string ToString() {
        return name;
    }

    public override int GetHashCode() {
        return name.GetHashCode();
    }
}

/// <summary>
/// A enum class containing the different architectures that Manila supports. Used to be exposed to the scripting environment.
/// </summary>
/// <param name="name">The stringified name of the architecture</param>
[ScriptEnum]
public class EArchitecture(string name) {
    public readonly string name = name;
    public static readonly EArchitecture X86 = new("x86");
    public static readonly EArchitecture X64 = new("x64");
    public static readonly EArchitecture Any = new("any");

    public static implicit operator string(EArchitecture a) {
        return a.name;
    }

    public override bool Equals(object? obj) {
        return obj is EArchitecture architecture && name == architecture.name;
    }
    public override string ToString() {
        return name;
    }
    public override int GetHashCode() {
        return name.GetHashCode();
    }
}
