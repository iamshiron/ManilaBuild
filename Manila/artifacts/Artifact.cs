
using Newtonsoft.Json;
using Shiron.Manila.API;
using Shiron.Manila.API.Builders;
using Shiron.Manila.Caching;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Logging;
using Shiron.Manila.Utils;

namespace Shiron.Manila.Artifacts;

public class Artifact(Workspace workspace, ArtifactBuilder builder) {
    public readonly string Description = builder.Description;
    public readonly Job[] Jobs = [.. builder.JobBuilders.Select(b => b.Build())];
    public readonly string Name = builder.Name ?? throw new ManilaException($"Artifact must have a name! {builder.Description}");
    public readonly UnresolvedProject Project = new UnresolvedProject(workspace, builder.Project.Name);
    public readonly RegexUtils.PluginComponentMatch PluginComponent = builder.PluginComponent ?? throw new ManilaException($"Artifact must have a plugin component match!");

    public LogCache? LogCache { get; internal set; } = null;

    public string GetFingerprint(BuildConfig config) => HashUtils.HashArtifact(this, config);
}
