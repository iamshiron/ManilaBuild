namespace Shiron.Manila.CPP;

using Shiron.Manila.Attributes;

[ScriptEnum]
public class EToolChain {
    public readonly string name;

    public EToolChain(string name) {
        this.name = name;
    }

    public static readonly EToolChain MSVC = new EToolChain("msvc");
    public static readonly EToolChain Clang = new EToolChain("clang");

    public override bool Equals(object? obj) {
        return obj is EToolChain toolChain && name == toolChain.name;
    }
    public override int GetHashCode() {
        return base.GetHashCode();
    }
}
