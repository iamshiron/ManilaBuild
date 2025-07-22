
namespace Shiron.Manila.Utils;

public interface IDirectories {
    string RootDir { get; }
    string DataDir { get; }
    string Plugins { get; }
    string Nuget { get; }
    string Profiles { get; }
    string Artifacts { get; }
    string Cache { get; }
    string Compiled { get; }

    IEnumerable<string> AllDataDirectories => [
        DataDir,
        Plugins,
        Nuget,
        Profiles,
        Artifacts,
        Cache,
        Compiled
    ];
}
