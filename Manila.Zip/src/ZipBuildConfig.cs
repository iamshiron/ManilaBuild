
using Shiron.Manila.API;
using Shiron.Manila.API.Attributes;

namespace Shiron.Manila.Zip;

[ManilaExpose]
public class ZipBuildConfig : BuildConfig {
    [FingerprintItem]
    public string? SubFolder { get; set; } = string.Empty;

    public string? GetSubFolder() {
        return SubFolder;
    }

    // Temporary till I am using reflection to set properties and add functions to the context
    public void SetSubFolder(string subFolder) {
        SubFolder = subFolder;
    }

    public override string ToString() {
        return $"ZipBuildConfig: SubFolder={SubFolder}";
    }
}
