
using Shiron.Manila.API.Interfaces;
using Shiron.Manila.API.Interfaces.Artifacts;
using Shiron.Manila.Interfaces;

namespace Shiron.Manila.API.Ext;

public abstract class LanguageComponent : PluginComponent {
    public readonly Type BuildConfigType;

    public LanguageComponent(string name, Type buildConfigType) : base(name) {
        BuildConfigType = buildConfigType;
    }
    public override string ToString() {
        return $"LanguageComponent({Name})";
    }

    public abstract IBuildExitCode Build(Workspace workspace, Project project, BuildConfig config, IArtifact artifact, IArtifactManager artifactManager);
    public abstract void Run(Project project);
}
