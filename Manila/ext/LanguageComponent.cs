using Shiron.Manila.API;
using Shiron.Manila.Artifacts;

namespace Shiron.Manila.Ext;

public abstract class LanguageComponent : PluginComponent {
    public readonly Type BuildConfigType;

    public LanguageComponent(string name, Type buildConfigType) : base(name) {
        BuildConfigType = buildConfigType;
    }
    public override string ToString() {
        return $"LanguageComponent({Name})";
    }

    public abstract IBuildExitCode Build(Workspace workspace, Project project, BuildConfig config, Artifact artifact, IArtifactManager artifactManager);
    public abstract void Run(Project project);
}
