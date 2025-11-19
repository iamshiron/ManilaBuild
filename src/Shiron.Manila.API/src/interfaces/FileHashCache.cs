
namespace Shiron.Manila.API.Interfaces;

/// <summary>
/// Defines the contract for a file hash cache.
/// </summary>
public interface IFileHashCache {
    /// <summary>
    /// Adds a new file hash to the cache or updates the existing hash for the given path.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <param name="hash">The file's hash.</param>
    void AddOrUpdate(string path, string hash);

    /// <summary>
    /// Checks if the hash for a given file path has changed.
    /// </summary>
    /// <param name="path">The file path to check.</param>
    /// <param name="hash">The current hash of the file.</param>
    /// <returns>True if the hash is different from the cached hash or if the file is not in the cache; otherwise, false.</returns>
    bool HasChanged(string path, string hash);

    /// <summary>
    /// Determines which files from a given collection have changed by comparing their current hashes to the cached hashes.
    /// </summary>
    /// <param name="paths">A collection of file paths to check.</param>
    /// <returns>A collection of paths for the files that have changed.</returns>
    Task<IEnumerable<string>> HasChangedAnyAsync(IEnumerable<string> paths);
}
