
using Shiron.Manila.API;
using Shiron.Manila.Exceptions;

namespace Shiron.Manila.Artifacts;

public class Artifact(ArtifactBuilder builder) {
    public readonly string Description = builder.Description;
    public readonly API.Task[] Tasks = [.. builder.TaskBuilders.Select(b => b.Build())];
    public readonly string Name = builder.Name ?? throw new ManilaException($"Artifact must have a name! {builder.Description}");
    public readonly string Root = ManilaEngine.GetInstance().ArtifactManager.GetArtifactRoot(builder.BuildConfig, builder.ProjectName, builder.Name);
    public readonly UnresolvedProject Project = new(builder.ProjectName);
}
