
using Shiron.Manila.Utils;

namespace Shiron.Manila.API;

public sealed class ArtifactBuilder(Action lambda) : IBuildable<Artifact> {
    public string Description = string.Empty;
    public readonly List<Task> Tasks = [];
    public readonly Action Lambda = lambda;

    public Artifact Build() {
        Lambda.Invoke();
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
}
