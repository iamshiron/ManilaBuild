namespace Shiron.Manila.CPP;

using Shiron.Manila.API;
using Shiron.Manila.CPP;

public class DependencyLink : Dependency {
    public string Path { get; private set; } = string.Empty;

    public DependencyLink() : base("link") {
    }

    public override void Create(params object[] args) {
        if (args.Length != 1) throw new Exception("Link dependency requires one argument");
        if (args[0] is not string) throw new Exception("Link dependency requires a string argument");
        this.Path = (string) args[0];
    }

    public override void Resolve() {
        ManilaCPP.Instance.Info("Resolving Dependency Link '" + this.Path + "'...");
    }
}
