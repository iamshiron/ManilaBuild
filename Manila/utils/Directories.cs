
public interface IDirectories {
    string RootDir { get; }
    string DataDir { get; }
    string Plugins { get; }
    string Nuget { get; }
    string Profiles { get; }
    string Artifacts { get; }
    string Cache { get; }
    string Compiled { get; }
}
