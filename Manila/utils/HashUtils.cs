using System.Collections;
using System.Security.Cryptography;
using System.Text;
using Shiron.Manila.API;

namespace Shiron.Manila.Utils;

public static class HashUtils {
    public static string HashFile(string file) {
        using var stream = File.OpenRead(file);
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(stream);
        return Convert.ToHexStringLower(hash);
    }
    public static string CreateFingerprint(IEnumerable<string> filePaths) {
        var sortedFiles = filePaths.OrderBy(p => p).ToList();
        var individualHashes = sortedFiles.Select(HashFile).ToList();
        var combinedHashes = string.Concat(individualHashes);
        var combinedBytes = Encoding.UTF8.GetBytes(combinedHashes);
        var finalHash = SHA256.HashData(combinedBytes);

        return Convert.ToHexStringLower(finalHash);
    }

    public static void HashArtifact(Artifact artifact) {
        var project = artifact.Project.Resolve();
    }
}
