namespace Shiron.Manila.Logging;

public static class HashUtils {
    public static string SHA256(string file) {
        using var stream = File.OpenRead(file);
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(stream);
        return Convert.ToHexStringLower(hash);
    }
}
