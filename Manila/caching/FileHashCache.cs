
using Microsoft.Data.Sqlite;
using Shiron.Manila.Utils;

namespace Shiron.Manila.Caching;

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
    Task<IEnumerable<string>> HasChangedAny(IEnumerable<string> paths);
}

/// <summary>
/// An implementation of IFileHashCache that uses a SQLite database for storage.
/// </summary>
public class FileHashCache : IFileHashCache {
    private readonly string _connectionString;
    private readonly string _root;

    public FileHashCache(string file, string root) {
        _root = root;
        _connectionString = $"Data Source={file};Mode=ReadWriteCreate;";

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS FileHashes (
                Path TEXT PRIMARY KEY,
                Hash TEXT NOT NULL
            );
        ";
        _ = command.ExecuteNonQuery();
    }

    /// <inheritdoc/>
    public void AddOrUpdate(string path, string hash) {
        if (Path.IsPathFullyQualified(path)) path = Path.GetRelativePath(_root, path);

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO FileHashes (Path, Hash)
            VALUES ($path, $hash)
            ON CONFLICT(Path) DO UPDATE SET Hash = $hash;
        ";

        _ = command.Parameters.AddWithValue("$path", path);
        _ = command.Parameters.AddWithValue("$hash", hash);

        _ = command.ExecuteNonQuery();
    }

    /// <inheritdoc/>
    public bool HasChanged(string path, string hash) {
        if (Path.IsPathFullyQualified(path)) path = Path.GetRelativePath(_root, path);

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT Hash FROM FileHashes WHERE Path = $path;
        ";

        _ = command.Parameters.AddWithValue("$path", path);

        var existingHash = command.ExecuteScalar() as string;

        return existingHash != hash;
    }

    public async Task<IEnumerable<string>> HasChangedAny(IEnumerable<string> paths) {
        paths = [.. paths
            .Select(p => Path.IsPathFullyQualified(p) ? Path.GetRelativePath(_root, p) : p)
            .Distinct()];

        var fileHahes = await HashUtils.CreateFileSetHashes(paths, _root);

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT Path, Hash FROM FileHashes WHERE Path IN ($paths);
        ";

        var pathList = string.Join(",", paths.Select(p => $"'{p}'"));
        command.CommandText = command.CommandText.Replace("$paths", pathList);
        using var reader = command.ExecuteReader();
        var changedFiles = new List<string>();

        var cachedHashes = new Dictionary<string, string>();
        while (reader.Read()) {
            var path = reader.GetString(0);
            var hash = reader.GetString(1);

            cachedHashes[path] = hash;
        }

        return fileHahes
            .Where(kvp => !cachedHashes.TryGetValue(kvp.Key, out var cachedHash) || cachedHash != kvp.Value)
            .Select(kvp => kvp.Key);
    }
}
