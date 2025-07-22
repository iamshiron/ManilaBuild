
using Shiron.Manila.Utils;

namespace Shiron.Manila.CLI;

public class Directories : IDirectories {
    public string RootDir => Directory.GetCurrentDirectory();
    public string DataDir => Path.Join(RootDir, ".manila");
    public string Plugins => Path.Join(DataDir, "plugins");
    public string Nuget => Path.Join(DataDir, "nuget");
    public string Profiles => Path.Join(DataDir, "profiles");
    public string Artifacts => Path.Join(DataDir, "artifacts");
    public string Cache => Path.Join(DataDir, "cache");
    public string Compiled => Path.Join(DataDir, "compiled");
}
