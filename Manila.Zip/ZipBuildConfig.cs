using System.Diagnostics.CodeAnalysis;
using Shiron.Manila.API;
using Shiron.Manila.Attributes;

namespace Shiron.Manila.Zip;

public class ZipBuildConfig : BuildConfig {
    [FingerprintItem]
    public string? SubFolder { get; set; } = string.Empty;

    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Exposed to JavaScript context")]
    public string? getSubFolder() {
        return SubFolder;
    }

    // Temporary till I am using reflection to set properties and add functions to the context
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Exposed to JavaScript context")]
    public void setSubFolder(string subFolder) {
        SubFolder = subFolder;
    }

    public override string ToString() {
        return $"ZipBuildConfig: SubFolder={SubFolder}";
    }
}
