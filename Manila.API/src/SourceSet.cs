
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Shiron.Manila.Utils;

namespace Shiron.Manila.API;

public class SourceSetBuilder(string root) : IBuildable<SourceSet> {
    public readonly string Root = root;
    public List<string> Includes { get; private set; } = [];
    public List<string> Excludes { get; private set; } = [];

    public SourceSetBuilder(string root, List<string> includes, List<string> excludes) : this(root) {
        Includes = includes;
        Excludes = excludes;
    }

    /// <summary>
    /// Include a list of files in the source set.
    /// </summary>
    /// <param name="globs">The pattern for the file matcher</param>
    /// <returns>SourceSet instance for chaining calls</returns>
    public SourceSetBuilder Include(params string[] globs) {
        Includes.AddRange(globs);
        return this;
    }
    /// <summary>
    /// Exclude a list of files from the source set.
    /// </summary>
    /// <param name="globs">The pattern for the file matcher</param>
    /// <returns>SourceSet instance for chaining calls</returns>
    public SourceSetBuilder Exclude(params string[] globs) {
        Excludes.AddRange(globs);
        return this;
    }

    /// <summary>
    /// Return a list of files in the source set.
    /// </summary>
    /// <returns>List of files complying to the includes and excludes</returns>
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

    public SourceSet Build() {
        return new(this);
    }
}

/// <summary>
/// Represents a set of files mostly used for source sets.
/// </summary>
public class SourceSet(SourceSetBuilder builder) {
    public readonly string Root = builder.Root;
    public readonly string[] Includes = [.. builder.Includes];
    public readonly string[] Excluded = [.. builder.Excludes];
    public readonly FileHandle[] FileHandles = [.. builder.Files()];
    public readonly string[] Files = [.. builder.Files().Select(f => f.Handle)];

    public long LastModified() {
        long lastModified = 0;
        foreach (var f in FileHandles) {
            lastModified = Math.Max(new DateTimeOffset(File.GetLastWriteTimeUtc(f)).ToUnixTimeMilliseconds(), lastModified);
        }
        return lastModified;
    }
    public string Fingerprint() {
        return HashUtils.CreateFileSetHash(Files);
    }
}
