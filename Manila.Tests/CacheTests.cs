using NUnit.Framework;

namespace Shiron.Manila.Caching.Tests;

/// <summary>
/// An in-memory implementation of IFileHashCache for testing purposes.
/// This class is not thread-safe for the testing property, but is for the cache itself.
/// </summary>
public class InMemoryFileHashCache : IFileHashCache {
    /// <summary>
    /// The internal in-memory cache for storing file paths and their corresponding hashes.
    /// Using ConcurrentDictionary for thread safety during cache operations.
    /// </summary>
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _cache = new();

    /// <summary>
    /// A mock source for what the "current" file hashes are.
    /// In a real scenario, these would be calculated from files on disk.
    /// For testing, you can populate this dictionary to simulate the current state of files
    /// before calling HasChangedAny.
    /// </summary>
    public IDictionary<string, string> CurrentFileHashesForTesting { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Adds a new file hash to the cache or updates it if the path already exists.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <param name="hash">The file's hash.</param>
    public void AddOrUpdate(string path, string hash) {
        _cache[path] = hash;
    }

    /// <summary>
    /// Checks if a single file has changed.
    /// A file is considered changed if its hash is different from the cached hash,
    /// or if the file is not present in the cache.
    /// </summary>
    /// <param name="path">The file path to check.</param>
    /// <param name="hash">The current hash of the file.</param>
    /// <returns>True if the file has changed; otherwise, false.</returns>
    public bool HasChanged(string path, string hash) {
        // If the path exists, compare its hash. If not, it's new, thus "changed".
        if (_cache.TryGetValue(path, out var existingHash)) {
            return existingHash != hash;
        }

        return true;
    }

    /// <summary>
    /// Determines which files from a given collection have changed.
    /// This implementation uses the `CurrentFileHashesForTesting` dictionary as the source
    /// for "current" file hashes, avoiding any file system access.
    /// </summary>
    /// <param name="paths">An enumerable of file paths to check.</param>
    /// <returns>A task that resolves to an enumerable of paths for the files that have changed.</returns>
    public Task<IEnumerable<string>> HasChangedAnyAsync(IEnumerable<string> paths) {
        // Filter the mock "current files" to only those in the input list.
        var filesToCheck = CurrentFileHashesForTesting
            .Where(kvp => paths.Contains(kvp.Key));

        // A file has changed if it's not in the cache, or if the hashes don't match.
        var changedFiles = filesToCheck
            .Where(kvp => !_cache.TryGetValue(kvp.Key, out var cachedHash) || cachedHash != kvp.Value)
            .Select(kvp => kvp.Key);

        return Task.FromResult(changedFiles);
    }

    /// <summary>
    /// Helper method for tests to retrieve a cached hash.
    /// </summary>
    /// <returns>The hash if the path is found; otherwise, null.</returns>
    public string? GetCachedHash(string path) {
        _ = _cache.TryGetValue(path, out var hash);
        return hash;
    }

    /// <summary>
    /// Helper method for tests to get the number of items in the cache.
    /// </summary>
    public int Count => _cache.Count;


    /// <summary>
    /// Helper method for tests to clear the cache state.
    /// </summary>
    public void Clear() {
        _cache.Clear();
        CurrentFileHashesForTesting.Clear();
    }
}


[TestFixture]
public class InMemoryFileHashCacheTests {
    private InMemoryFileHashCache _cache;

    // Using constants to avoid magic strings. Cleaner.
    private const string _file1 = "file1.txt";
    private const string _file2 = "file2.txt";
    private const string _file3 = "file3.txt";

    private const string _hashV1 = "hash_v1";
    private const string _hashV2 = "hash_v2";
    private const string _hashV3 = "hash_v3";
    private const string _hashV1Updated = "hash_v1_updated";

    [SetUp]
    public void SetUp() {
        // Initialize a fresh cache before each test. Keeps things isolated.
        _cache = new InMemoryFileHashCache();
    }

    #region HasChanged Tests

    [Test]
    public void HasChanged_WhenFileIsNew_ReturnsTrue() {
        // A file not in the cache is always considered changed.
        Assert.That(_cache.HasChanged(_file1, _hashV1), Is.True);
    }

    [Test]
    public void HasChanged_WhenHashIsIdentical_ReturnsFalse() {
        _cache.AddOrUpdate(_file1, _hashV1);

        // Hashes match. No change.
        Assert.That(_cache.HasChanged(_file1, _hashV1), Is.False);
    }

    [Test]
    public void HasChanged_WhenHashIsDifferent_ReturnsTrue() {
        _cache.AddOrUpdate(_file1, _hashV1);

        // New hash is different from cached hash. It's changed.
        Assert.That(_cache.HasChanged(_file1, _hashV1Updated), Is.True);
    }

    #endregion

    #region HasChangedAny Tests

    [Test]
    public async Task HasChangedAny_WithMixedChanges_ReturnsOnlyChangedFiles() {
        // Setup: Cache has v1 of file1 and v2 of file2.
        _cache.AddOrUpdate(_file1, _hashV1);
        _cache.AddOrUpdate(_file2, _hashV2);

        // Current state: file1 has a new hash, file2 is the same, file3 is new.
        _cache.CurrentFileHashesForTesting = new Dictionary<string, string>
        {
            { _file1, _hashV1Updated },
            { _file2, _hashV2 },
            { _file3, _hashV3 }
        };

        var pathsToCheck = new[] { _file1, _file2, _file3 };
        var changedFiles = (await _cache.HasChangedAnyAsync(pathsToCheck)).ToList();

        // Expect file1 (updated) and file3 (new) to be reported.
        // file2 is unchanged and should be ignored.
        Assert.That(changedFiles, Has.Count.EqualTo(2));
        Assert.That(changedFiles, Contains.Item(_file1));
        Assert.That(changedFiles, Contains.Item(_file3));
    }

    [Test]
    public async Task HasChangedAny_WhenNoFilesChange_ReturnsEmpty() {
        _cache.AddOrUpdate(_file1, _hashV1);
        _cache.AddOrUpdate(_file2, _hashV2);

        _cache.CurrentFileHashesForTesting = new Dictionary<string, string>
        {
            { _file1, _hashV1 },
            { _file2, _hashV2 }
        };

        var pathsToCheck = new[] { _file1, _file2 };
        var changedFiles = await _cache.HasChangedAnyAsync(pathsToCheck);

        // No changes, should return an empty collection.
        Assert.That(changedFiles, Is.Empty);
    }

    [Test]
    public async Task HasChangedAny_WithEmptyInput_ReturnsEmpty() {
        // Give it nothing, expect nothing back.
        var changedFiles = await _cache.HasChangedAnyAsync(Enumerable.Empty<string>());
        Assert.That(changedFiles, Is.Empty);
    }

    [Test]
    public async Task HasChangedAny_WhenFileIsRemovedFromSource_IsNotReportedAsChanged() {
        // file1 exists in the cache but is not in the "current" files list.
        // This simulates a deleted file.
        _cache.AddOrUpdate(_file1, _hashV1);
        _cache.CurrentFileHashesForTesting = new Dictionary<string, string>
        {
            { _file2, _hashV2 } // Only file2 is present.
        };

        var pathsToCheck = new[] { _file1, _file2 };
        var changedFiles = (await _cache.HasChangedAnyAsync(pathsToCheck)).ToList();

        // Only the new file (file2) should be reported.
        // The deleted file (file1) should not be in the result.
        Assert.That(changedFiles, Has.Count.EqualTo(1));
        Assert.That(changedFiles, Contains.Item(_file2));
    }

    #endregion

    #region State Management Tests

    [Test]
    public void AddOrUpdate_ShouldAddNewAndModifyExistingEntries() {
        // Test adding a new entry.
        _cache.AddOrUpdate(_file1, _hashV1);
        Assert.That(_cache.Count, Is.EqualTo(1));
        Assert.That(_cache.GetCachedHash(_file1), Is.EqualTo(_hashV1));

        // Test updating the existing entry.
        _cache.AddOrUpdate(_file1, _hashV1Updated);
        Assert.That(_cache.Count, Is.EqualTo(1)); // Count should not change.
        Assert.That(_cache.GetCachedHash(_file1), Is.EqualTo(_hashV1Updated));
    }

    [Test]
    public void Clear_ShouldRemoveAllEntries() {
        _cache.AddOrUpdate(_file1, _hashV1);
        _cache.AddOrUpdate(_file2, _hashV2);

        Assert.That(_cache.Count, Is.EqualTo(2));

        _cache.Clear();

        // After clearing, cache should be empty.
        Assert.That(_cache.Count, Is.EqualTo(0));
    }

    #endregion
}
