
namespace Shiron.Manila.Utils;

public static class TimeUtils {
    /// <summary>
    /// Gets the current time in milliseconds since the Unix epoch (January 1, 1970).
    /// </summary>
    public static long Now() {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}
