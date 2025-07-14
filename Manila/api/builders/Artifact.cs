
using Shiron.Manila.Artifacts;
using Shiron.Manila.Utils;

namespace Shiron.Manila.API.Builders;

public sealed class ArtifactBuilder(Action lambda, Manila manilaAPI, BuildConfig buildConfig, string projectName) : IBuildable<Artifact> {
    public readonly BuildConfig BuildConfig = buildConfig;
    public readonly string ProjectName = projectName;
    public string Description = string.Empty;
    public readonly List<TaskBuilder> TaskBuilders = [];
    public readonly Action Lambda = lambda;
    public readonly Manila ManilaAPI = manilaAPI;
    public string? Name = null;

    public Artifact Build() {
        ManilaAPI.CurrentArtifactBuilder = this;
        Lambda.Invoke();
        ManilaAPI.CurrentArtifactBuilder = null;
        return new(this);
    }

    public ArtifactBuilder description(string description) {
        Description = description;
        return this;
    }
    public ArtifactBuilder dependencies() {
        return this;
    }
}
