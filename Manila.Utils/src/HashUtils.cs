using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Shiron.Manila.Logging;
using Shiron.Manila.Profiling;

namespace Shiron.Manila.Utils;

public static class HashUtils {
    public static string HashFile(string file, IProfiler? profiler = null) {
        IDisposable? disposable = null;
        if (profiler is not null) disposable = new ProfileScope(profiler, MethodBase.GetCurrentMethod()!);

        using var stream = File.OpenRead(file);
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(stream);

        disposable?.Dispose();
        return Convert.ToHexStringLower(hash);
    }

    public static string CreateFileSetHash(IEnumerable<string> filePaths, string? root = null, IProfiler? profiler = null) {
        ProfileScope? disposable = null;
        if (profiler is not null) disposable = new ProfileScope(profiler, MethodBase.GetCurrentMethod()!);

        var sortedFiles = filePaths.OrderBy(p => p).ToList();
        var individualHashes = sortedFiles.Select(path => {
            var filePath = root is not null ? Path.GetRelativePath(root, path) : path;
            var fileHash = HashFile(path);
            var pathHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(filePath)));
            return CombineHashes([fileHash, pathHash]);
        }).ToList();
        var combinedHashes = string.Concat(individualHashes);
        var combinedBytes = Encoding.UTF8.GetBytes(combinedHashes);
        var finalHash = SHA256.HashData(combinedBytes);

        return Convert.ToHexStringLower(finalHash);
    }

    /// <summary>
    /// Creates a dictionary of file paths and their corresponding hashes.
    /// The keys are the file paths relative to the specified root, and the values are the
    /// SHA256 hashes of the files.
    /// </summary>
    /// <param name="files">The collection of file paths to hash.</param>
    /// <param name="root">The root directory to use for relative paths. If null, absolute paths are used.</param>
    /// <returns></returns>
    public static async Task<Dictionary<string, string>> CreateFileSetHashesAsync(IEnumerable<string> files, string? root = null, IProfiler? profiler = null) {
        ProfileScope? disposable = null;
        if (profiler is not null) disposable = new ProfileScope(profiler, MethodBase.GetCurrentMethod()!);

        var result = new ConcurrentDictionary<string, string>();
        await Parallel.ForEachAsync(files, async (file, token) => {
            var filePath = root is not null ? Path.GetRelativePath(root, file) : file;
            var fileHash = await Task.Run(() => HashFile(file), token);
            _ = result.TryAdd(filePath, fileHash);
        });

        disposable?.Dispose();
        return result.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    public static string CombineHashes(IEnumerable<string> hashes, IProfiler? profiler = null) {
        ProfileScope? disposable = null;
        if (profiler is not null) disposable = new ProfileScope(profiler, MethodBase.GetCurrentMethod()!);

        var combined = string.Concat(hashes.OrderBy(h => h));
        var combinedBytes = Encoding.UTF8.GetBytes(combined);
        var finalHash = SHA256.HashData(combinedBytes);

        disposable?.Dispose();
        return Convert.ToHexStringLower(finalHash);
    }
}
