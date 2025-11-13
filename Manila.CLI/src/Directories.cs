
using Shiron.Manila.Utils;

namespace Shiron.Manila.CLI;

/// <summary>
/// Resolves Manila working directory layout
/// </summary>
public class Directories : IDirectories {
    /// <summary>
    /// Workspace root (current directory)
    /// </summary>
    public string Root => Directory.GetCurrentDirectory();
    /// <summary>
    /// Root data folder (.manila)
    /// </summary>
    public string Data => Path.Join(Root, ".manila");
    /// <summary>
    /// Plugins folder path
    /// </summary>
    public string Plugins => Path.Join(Data, "plugins");
    /// <summary>
    /// NuGet packages folder path
    /// </summary>
    public string Nuget => Path.Join(Data, "nuget");
    /// <summary>
    /// Profiles folder path
    /// </summary>
    public string Profiles => Path.Join(Data, "profiles");
    /// <summary>
    /// Artifacts folder path
    /// </summary>
    public string Artifacts => Path.Join(Data, "artifacts");
    /// <summary>
    /// Cache folder path
    /// </summary>
    public string Cache => Path.Join(Data, "cache");
    /// <summary>
    /// Compiled scripts folder path
    /// </summary>
    public string Compiled => Path.Join(Data, "compiled");
}
