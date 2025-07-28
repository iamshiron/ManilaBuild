
namespace Shiron.Manila.API.Interfaces;

public interface INuGetManager {
    Task<List<string>> DownloadPackageWithDependenciesAsync(string packageId, string version);
    Task LoadCacheAsync();
    Task PersistCacheAsync();
}
