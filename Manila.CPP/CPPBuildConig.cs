
using System.Diagnostics.CodeAnalysis;
using Shiron.Manila.API;
using Shiron.Manila.Attributes;

namespace Shiron.Manila.CPP;

public class CPPBuildConfig : BuildConfig {
    [ArtifactKey]
    public string Config { get; set; } = "Debug";

    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Exposed to JavaScript context")]
    public string getConfig() {
        return Config;
    }
}
