
using Shiron.Manila.API;
using Shiron.Manila.API.Builders;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Utils;

namespace Shiron.Manila.Artifacts;

public class Artifact(ArtifactBuilder builder) {
    public readonly string Description = builder.Description;
    public readonly Job[] Jobs = [.. builder.JobBuilders.Select(b => b.Build())];
    public readonly string Name = builder.Name ?? throw new ManilaException($"Artifact must have a name! {builder.Description}");
    public readonly string Root = ManilaEngine.GetInstance().ArtifactManager.GetArtifactRoot(builder.BuildConfig, builder.ProjectName, builder.Name);
    public readonly UnresolvedProject Project = new(builder.ProjectName);
    public readonly RegexUtils.PluginComponentMatch PluginComponent = builder.PluginComponent ?? throw new ManilaException($"Artifact must have a plugin component match!");

    public string GetFingerprint(BuildConfig config) {
        return HashUtils.HashArtifact(this, config);
    }
}
