
using Shiron.Manila.Exceptions;
using Shiron.Manila.Utils;

namespace Shiron.Manila.API;

public sealed class ArtifactBuilder(Action lambda, Manila manilaAPI) : IBuildable<Artifact> {
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

public class Artifact(ArtifactBuilder builder) {
    public readonly string Description = builder.Description;
    public readonly Task[] Tasks = [.. builder.TaskBuilders.Select(b => b.Build())];
    public readonly string Name = builder.Name ?? throw new ManilaException($"Artifact must have a name! {builder.Description}");
}
