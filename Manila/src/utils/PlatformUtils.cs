
using System.Runtime.InteropServices;
using Shiron.Manila.Exceptions;

namespace Shiron.Manila.Utils;

public static class PlatformUtils {
    public static string GetPlatformKey() {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "windows";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return "linux";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return "osx";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD)) return "freebsd";
        throw new ManilaException("Unable to determine platform!");
    }

    public static string GetArchitectureKey() {
        return RuntimeInformation.OSArchitecture.ToString().ToLower();
    }
}
