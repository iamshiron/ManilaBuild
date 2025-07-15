
using System.Diagnostics.CodeAnalysis;
using Shiron.Manila.Attributes;

namespace Shiron.Manila.CPP;

[ScriptEnum]
public class EToolChain(string name) {
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Exposed to JavaScript context")]
    public readonly string name = name;

    public static readonly EToolChain MSVC = new("msvc");
    public static readonly EToolChain Clang = new("clang");

    public override bool Equals(object? obj) {
        return obj is EToolChain toolChain && name == toolChain.name;
    }
    public override int GetHashCode() {
        return base.GetHashCode();
    }
}
