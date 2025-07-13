
using Shiron.Manila.API;
using Shiron.Manila.Attributes;

namespace Shiron.Manila.CPP;

public class CPPBuildConfig : BuildConfig {
    [ArtifactKey]
    public string Config { get; set; } = "Debug";

    public string getConfig() {
        return Config;
    }
}
