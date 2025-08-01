namespace Shiron.Manila.Utils;

public interface IDirectories {
    string Root { get; }
    string Data { get; }
    string Plugins { get; }
    string Nuget { get; }
    string Profiles { get; }
    string Artifacts { get; }
    string Cache { get; }
    string Compiled { get; }

    IEnumerable<string> AllDataDirectories => [
        Data,
        Plugins,
        Nuget,
        Profiles,
        Artifacts,
        Cache,
        Compiled
    ];
}
