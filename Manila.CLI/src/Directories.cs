
using Shiron.Manila.Utils;

namespace Shiron.Manila.CLI;

public class Directories : IDirectories {
    public string Root => Directory.GetCurrentDirectory();
    public string Data => Path.Join(Root, ".manila");
    public string Plugins => Path.Join(Data, "plugins");
    public string Nuget => Path.Join(Data, "nuget");
    public string Profiles => Path.Join(Data, "profiles");
    public string Artifacts => Path.Join(Data, "artifacts");
    public string Cache => Path.Join(Data, "cache");
    public string Compiled => Path.Join(Data, "compiled");
    public string Temp => Path.Join(Data, "temp");
}
