using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace Shiron.Manila.API;

/// <summary>
/// Represents a set of files mostly used for source sets.
/// </summary>
public class SourceSet {
    public string Root { get; private set; }
    public List<string> Includes { get; private set; } = new();
    public List<string> Excludes { get; private set; } = new();

    public SourceSet(string root) {
        this.Root = root;
    }

    /// <summary>
    /// Include a list of files in the source set.
    /// </summary>
    /// <param name="globs">The pattern for the file matcher</param>
    /// <returns>SourceSet instance for chaining calls</returns>
    public SourceSet include(params string[] globs) {
        Includes.AddRange(globs);
        return this;
    }
    /// <summary>
    /// Exclude a list of files from the source set.
    /// </summary>
    /// <param name="globs">The pattern for the file matcher</param>
    /// <returns>SourceSet instance for chaining calls</returns>
    public SourceSet exclude(params string[] globs) {
        Excludes.AddRange(globs);
        return this;
    }

    /// <summary>
    /// Return a list of files in the source set.
    /// </summary>
    /// <returns>List of files complying to the includes and excludes</returns>
    public File[] files() {
        var matcher = new Matcher();
        foreach (var include in Includes) {
            matcher.AddInclude(include);
        }
        foreach (var exclude in Excludes) {
            matcher.AddExclude(exclude);
        }

        var result = matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(Root)));
        return result.Files.Select(f => new File(f.Path)).ToArray();
    }
}
