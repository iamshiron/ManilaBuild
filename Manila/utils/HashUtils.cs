using System.Collections;
using System.Security.Cryptography;
using System.Text;

namespace Shiron.Manila.Utils;

public static class HashUtils {
    public static string HashFile(string file) {
        using var stream = File.OpenRead(file);
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(stream);
        return Convert.ToHexStringLower(hash);
    }
}
