namespace Shiron.Manila.API;

/// <summary>
/// A storage class for build configurations. Used to be exposed to the scripting environment.
/// </summary>
public class BuildConfig {
    public BuildConfig() {
        config = "Debug";
        platform = EPlatform.Windows;
        architecture = EArchitecture.X64;
    }

    public string config { get; set; }
    public EPlatform platform { get; set; }
    public EArchitecture architecture { get; set; }

    // Functions that are mostly used by scripts\
    public string getConfig() {
        return config;
    }
    public string getPlatform() {
        return platform;
    }
    public string getArchitecture() {
        return architecture;
    }
}
