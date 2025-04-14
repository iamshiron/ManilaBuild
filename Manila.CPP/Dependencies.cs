namespace Shiron.Manila.CPP;

using Shiron.Manila.API;

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

public class DependencyProject : Dependency {
    public UnresolvedProject Project { get; private set; } = null!;
    public string BuildTask { get; private set; } = string.Empty;

    public DependencyProject() : base("project") {
    }

    public override void Create(params object[] args) {
        if (args.Length != 2) throw new Exception("Project dependency requires two arguments");
        if (args[0] is not string || args[1] is not string) throw new Exception("Project dependency requires 2 string arguments");
        this.Project = new UnresolvedProject((string) args[0]);
        this.BuildTask = (string) args[1];
    }

    public override void Resolve() {
        ManilaCPP.Instance.Info(string.Join(", ", ManilaEngine.GetInstance().Workspace.Projects.Keys));

        var project = this.Project.Resolve();
        ManilaCPP.Instance.Info("Resolving Dependency Project '" + project.GetIdentifier() + "'...");
    }
}
