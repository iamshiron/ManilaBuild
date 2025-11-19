using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Shiron.Manila.API.Interfaces;
using Shiron.Utils;

namespace Shiron.Manila.API;

/// <summary>Builder for file source set.</summary>
/// <param name="root">Root directory path.</param>
public class SourceSetBuilder(string root) : IBuildable<SourceSet> {
    /// <summary>Root directory.</summary>
    public readonly string Root = root;
    /// <summary>Include glob patterns.</summary>
    public List<string> Includes { get; private set; } = [];
    /// <summary>Exclude glob patterns.</summary>
    public List<string> Excludes { get; private set; } = [];

    public SourceSetBuilder(string root, List<string> includes, List<string> excludes) : this(root) {
        Includes = includes;
        Excludes = excludes;
    }

    /// <summary>Add include glob patterns.</summary>
    /// <param name="globs">Glob strings.</param>
    /// <returns>Builder (chain).</returns>
    public SourceSetBuilder Include(params string[] globs) {
        Includes.AddRange(globs);
        return this;
    }
    /// <summary>Add exclude glob patterns.</summary>
    /// <param name="globs">Glob strings.</param>
    /// <returns>Builder (chain).</returns>
    public SourceSetBuilder Exclude(params string[] globs) {
        Excludes.AddRange(globs);
        return this;
    }

    /// <summary>Resolve matched files.</summary>
    /// <returns>File handles.</returns>
    public FileHandle[] Files() {
        var matcher = new Matcher();
        foreach (var include in Includes) {
            matcher.AddInclude(include);
        }
        foreach (var exclude in Excludes) {
            matcher.AddExclude(exclude);
        }

        var result = matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(Root)));
        return result.Files.Select(f => new FileHandle(Root, f.Path)).ToArray();
    }

    /// <summary>Build immutable source set.</summary>
    /// <returns>Source set.</returns>
    public SourceSet Build() => new(this);
}

/// <summary>Immutable resolved source file set.</summary>
/// <param name="builder">Source set builder.</param>
public class SourceSet(SourceSetBuilder builder) {
    /// <summary>Root directory.</summary>
    public readonly string Root = builder.Root;
    /// <summary>Include patterns.</summary>
    public readonly string[] Includes = [.. builder.Includes];
    /// <summary>Exclude patterns.</summary>
    public readonly string[] Excluded = [.. builder.Excludes];
    /// <summary>Resolved file handles.</summary>
    public readonly FileHandle[] FileHandles = [.. builder.Files()];
    /// <summary>Resolved file paths.</summary>
    public readonly string[] Files = [.. builder.Files().Select(f => f.Handle)];

    /// <summary>Latest last-write time (ms).</summary>
    /// <returns>Unix ms timestamp.</returns>
    public long LastModified() {
        long lastModified = 0;
        foreach (var f in FileHandles) {
            lastModified = Math.Max(new DateTimeOffset(File.GetLastWriteTimeUtc(f)).ToUnixTimeMilliseconds(), lastModified);
        }
        return lastModified;
    }
    /// <summary>Deterministic hash over file paths.</summary>
    /// <returns>SHA256 hex string.</returns>
    public string Fingerprint() => HashUtils.CreateFileSetHash(Files);
}
