using System.Reflection;
using System.IO.Compression;
using System.Runtime.Versioning;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using System.Runtime.Loader;
using Shiron.Manila.Logging;
using System.Text.Json;
using Shiron.Manila.Profiling;

namespace Shiron.Manila.Utils;

/// <summary>
/// Represents a cached NuGet package with its dependencies and assembly paths.
/// </summary>
public class InstalledPackage {
    public List<string> Dependencies { get; set; } = [];
    public List<string> DLLPaths { get; set; } = [];
}

/// <summary>
/// Manages downloading, caching, and resolving NuGet packages and their dependencies.
/// </summary>
public class NuGetManager {
    public readonly string PackageDir;
    public readonly string PackageCacheFilePath;

    // Caches package information. Key: "PackageID@Version", Value: Details about the package.
    private Dictionary<string, InstalledPackage> _installedPackages = [];
    // Caches resolved package dependencies to avoid redundant API calls. Key: "PackageID@Version".
    private readonly Dictionary<string, SourcePackageDependencyInfo> _resolvedPackageCache = [];
    private List<string>? _basePackages;

    private readonly SourceRepository _repository;
    private readonly DependencyInfoResource _dependencyResource;
    private readonly FindPackageByIdResource _findPackageByIdResource;
    private readonly NuGetFramework _currentTargetFramework;

    /// <summary>
    /// Initializes a new instance of the <see cref="NuGetManager"/> class.
    /// </summary>
    /// <param name="packageDir">The directory to store downloaded packages.</param>
    public NuGetManager(string packageDir) {
        using (new ProfileScope(MethodBase.GetCurrentMethod()!)) {
            PackageDir = packageDir;
            PackageCacheFilePath = Path.Combine(PackageDir, "nuget.json");
            Directory.CreateDirectory(PackageDir); // Ensures the directory exists.

            _repository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
            _dependencyResource = _repository.GetResource<DependencyInfoResource>();
            _findPackageByIdResource = _repository.GetResource<FindPackageByIdResource>();
            _currentTargetFramework = NuGetFramework.Parse(GetCurrentFrameworkName());

            LoadCache();
        }
    }

    private void LoadCache() {
        if (!File.Exists(PackageCacheFilePath)) return;
        try {
            var serializedData = File.ReadAllText(PackageCacheFilePath);
            var data = JsonSerializer.Deserialize<Dictionary<string, InstalledPackage>>(serializedData);
            if (data != null) {
                _installedPackages = data;
            }
        } catch (Exception e) {
            Logger.Warning($"Unable to read NuGet package cache at '{PackageCacheFilePath}'. A new cache will be created. Reason: {e.Message}");
        }
    }

    public void PersistCache() {
        try {
            var serializedData = JsonSerializer.Serialize(_installedPackages, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(PackageCacheFilePath, serializedData);
        } catch (Exception e) {
            Logger.Warning("Unable to write NuGet package cache! " + e.Message);
        }
    }

    /// <summary>
    /// Downloads a NuGet package and all its dependencies, returning the paths to all relevant DLLs.
    /// </summary>
    /// <param name="packageId">The ID of the package to download.</param>
    /// <param name="version">The version of the package to download.</param>
    /// <returns>A list of absolute file paths to the downloaded DLLs.</returns>
    public async Task<List<string>> DownloadPackageWithDependenciesAsync(string packageId, string version) {
        using (new ProfileScope(MethodBase.GetCurrentMethod()!)) {
            string cacheKey = $"{packageId}@{version}";
            if (_installedPackages.TryGetValue(cacheKey, out var cachedPackage)) {
                // Reconstruct absolute paths from cached relative paths.
                return [.. cachedPackage.DLLPaths.Select(p => Path.Combine(PackageDir, p))];
            }

            var allPackages = new HashSet<SourcePackageDependencyInfo>(PackageIdentityComparer.Default);
            await WalkDependencyTreeAsync(packageId, new NuGetVersion(version), allPackages).ConfigureAwait(false);

            var allDllPaths = new List<string>();
            var topLevelPackage = new InstalledPackage();

            foreach (var package in allPackages) {
                topLevelPackage.Dependencies.Add($"{package.Id}@{package.Version}");
                string packageCacheKey = $"{package.Id}@{package.Version}";

                List<string> packageDlls;
                if (_installedPackages.TryGetValue(packageCacheKey, out var existingPackage)) {
                    packageDlls = existingPackage.DLLPaths.Select(p => Path.Combine(PackageDir, p)).ToList();
                } else {
                    var nupkgPath = await DownloadPackageAsync(package.Id, package.Version).ConfigureAwait(false);
                    var extractPath = Path.Combine(PackageDir, $"{package.Id}_{package.Version.ToNormalizedString()}");
                    packageDlls = GetAssemblyPaths(nupkgPath, extractPath);

                    // Store this individual package's info with relative paths for persistence.
                    var newPackageEntry = new InstalledPackage {
                        DLLPaths = packageDlls.Select(p => Path.GetRelativePath(PackageDir, p)).ToList()
                    };
                    _installedPackages[packageCacheKey] = newPackageEntry;
                }
                allDllPaths.AddRange(packageDlls);
            }

            // Cache the top-level package with all its transitive DLLs.
            topLevelPackage.DLLPaths = allDllPaths.Select(p => Path.GetRelativePath(PackageDir, p)).ToList();
            _installedPackages[cacheKey] = topLevelPackage;

            return allDllPaths;
        }
    }

    private async Task WalkDependencyTreeAsync(string packageId, NuGetVersion version, HashSet<SourcePackageDependencyInfo> collectedPackages) {
        using (new ProfileScope(MethodBase.GetCurrentMethod()!)) {
            _basePackages ??= GetInstalledBasePackages();
            if (packageId.StartsWith("System") || packageId.StartsWith("Microsoft") || _basePackages.Contains(packageId)) return;

            var package = await ResolvePackageAsync(packageId, version).ConfigureAwait(false);
            if (package == null || !collectedPackages.Add(package)) {
                return;
            }

            foreach (var dependency in package.Dependencies) {
                if (dependency.VersionRange.MinVersion != null) {
                    await WalkDependencyTreeAsync(dependency.Id, dependency.VersionRange.MinVersion, collectedPackages).ConfigureAwait(false);
                }
            }
        }
    }

    private async Task<SourcePackageDependencyInfo?> ResolvePackageAsync(string packageId, NuGetVersion version) {
        var key = $"{packageId}@{version}";
        if (_resolvedPackageCache.TryGetValue(key, out var cachedInfo)) {
            return cachedInfo;
        }

        var packageIdentity = new PackageIdentity(packageId, version);
        var packageInfo = await _dependencyResource.ResolvePackage(packageIdentity, _currentTargetFramework, new SourceCacheContext(), NullLogger.Instance, CancellationToken.None).ConfigureAwait(false);

        if (packageInfo != null) {
            _resolvedPackageCache[key] = packageInfo;
        }
        return packageInfo;
    }

    private async Task<string> DownloadPackageAsync(string id, NuGetVersion version) {
        var nupkgFileName = $"{id}_{version.ToNormalizedString()}.nupkg";
        var downloadPath = Path.Combine(PackageDir, nupkgFileName);
        if (File.Exists(downloadPath)) return downloadPath;

        using var packageStream = new MemoryStream();
        bool success = await _findPackageByIdResource.CopyNupkgToStreamAsync(id, version, packageStream, new SourceCacheContext(), NullLogger.Instance, CancellationToken.None).ConfigureAwait(false);

        if (!success) throw new InvalidOperationException($"Failed to download package: {id}@{version}");

        using var fileStream = new FileStream(downloadPath, FileMode.Create, FileAccess.Write);
        packageStream.Position = 0;
        await packageStream.CopyToAsync(fileStream).ConfigureAwait(false);

        return downloadPath;
    }

    private List<string> GetAssemblyPaths(string nupkgPath, string extractPath) {
        if (!Directory.Exists(extractPath) || !Directory.EnumerateFileSystemEntries(extractPath).Any()) {
            ZipFile.ExtractToDirectory(nupkgPath, extractPath, overwriteFiles: true);
        }

        var libPath = Path.Combine(extractPath, "lib");
        if (!Directory.Exists(libPath)) return [];

        var frameworkReducer = new FrameworkReducer();
        var availableFrameworks = Directory.EnumerateDirectories(libPath).Select(dir => NuGetFramework.Parse(Path.GetFileName(dir)));
        var bestFramework = frameworkReducer.GetNearest(_currentTargetFramework, availableFrameworks);

        if (bestFramework == null) return [];

        var bestLibDir = Path.Combine(libPath, bestFramework.GetShortFolderName());
        return Directory.EnumerateFiles(bestLibDir, "*.dll").ToList();
    }

    private static string GetCurrentFrameworkName()
        => Assembly.GetEntryAssembly()?.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName
           ?? throw new InvalidOperationException("Could not determine the application's target framework.");

    private static List<string> GetInstalledBasePackages() {
        using (new ProfileScope(MethodBase.GetCurrentMethod()!)) {
            var basePackages = new List<string>();
            var entryAssembly = Assembly.GetEntryAssembly();
            if (entryAssembly == null) {
                Logger.Warning("Could not get entry assembly. Unable to populate installed packages, loading will be slower.");
                return basePackages;
            }

            var depsFilePath = Path.Combine(AppContext.BaseDirectory, $"{entryAssembly.GetName().Name}.deps.json");
            if (!File.Exists(depsFilePath)) {
                Logger.Warning("Could not find .deps.json file. Unable to populate installed packages, loading will be slower.");
                return basePackages;
            }

            using var jsonDocument = JsonDocument.Parse(File.ReadAllText(depsFilePath));
            if (jsonDocument.RootElement.TryGetProperty("libraries", out var libraries)) {
                foreach (var library in libraries.EnumerateObject()) {
                    if (library.Value.TryGetProperty("type", out var type) && type.GetString() == "package") {
                        basePackages.Add(library.Name.Split('/')[0]);
                    }
                }
            }

            Logger.Debug($"Found {basePackages.Count} installed base packages.");
            return basePackages;
        }
    }
}

/// <summary>
/// A custom AssemblyLoadContext for loading plugin assemblies and their dependencies.
/// </summary>
public class PluginLoadContext(string pluginPath) : AssemblyLoadContext {
    private readonly AssemblyDependencyResolver _resolver = new(pluginPath);
    private readonly Dictionary<string, string> _dependencyMap = [];

    /// <summary>
    /// Adds a NuGet dependency path to the context's internal resolution map.
    /// </summary>
    /// <param name="path">The full path to the dependency DLL.</param>
    public void AddDependency(string path) {
        using (new ProfileScope(MethodBase.GetCurrentMethod()!)) {
            string assemblyName = Path.GetFileNameWithoutExtension(path);
            _dependencyMap.TryAdd(assemblyName, path);
        }
    }

    /// <summary>
    /// Tries to load an assembly by its name, checking the default resolver and then the custom dependency map.
    /// </summary>
    /// <param name="assemblyName">The name of the assembly to load.</param>
    /// <returns>The loaded Assembly, or null if it could not be found.</returns>
    public Assembly? TryLoad(AssemblyName assemblyName) {
        using (new ProfileScope(MethodBase.GetCurrentMethod()!)) {
            // First, try to resolve using the plugin's own dependency resolver.
            string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            if (assemblyPath != null) {
                return LoadFromAssemblyPath(assemblyPath);
            }

            // Next, try to resolve from the manually added NuGet dependencies.
            if (assemblyName.Name != null && _dependencyMap.TryGetValue(assemblyName.Name, out string? mappedPath)) {
                return LoadFromAssemblyPath(mappedPath);
            }

            return null;
        }
    }

    protected override Assembly? Load(AssemblyName assemblyName) {
        using (new ProfileScope(MethodBase.GetCurrentMethod()!)) {
            // Defer system and core Microsoft assemblies to the default AssemblyLoadContext.
            if (assemblyName.Name != null && (assemblyName.Name.StartsWith("System.") || assemblyName.Name.StartsWith("Microsoft."))) {
                return null;
            }

            return TryLoad(assemblyName);
        }
    }
}


/// <summary>
/// Manages multiple PluginLoadContexts and resolves assemblies across them.
/// </summary>
public static class PluginContextManager {
    private static readonly List<PluginLoadContext> _contexts = [];
    private static bool _isHandlerRegistered = false;

    /// <summary>
    /// Adds a new plugin context to the manager and registers the resolving event handler if needed.
    /// </summary>
    /// <param name="context">The PluginLoadContext to add.</param>
    public static void AddContext(PluginLoadContext context) {
        using (new ProfileScope(MethodBase.GetCurrentMethod()!)) {
            _contexts.Add(context);

            if (!_isHandlerRegistered) {
                AssemblyLoadContext.Default.Resolving += OnDefaultContextResolving;
                _isHandlerRegistered = true;
            }
        }
    }

    /// <summary>
    /// Handles assembly resolution for the default context by checking all registered plugin contexts.
    /// This allows assemblies loaded in one plugin's context to be found by others.
    /// </summary>
    private static Assembly? OnDefaultContextResolving(AssemblyLoadContext context, AssemblyName name) {
        using (new ProfileScope(MethodBase.GetCurrentMethod()!)) {
            // Iterate through all known plugin contexts to find the requested assembly.
            foreach (var pluginContext in _contexts) {
                var assembly = pluginContext.TryLoad(name);
                if (assembly != null) {
                    return assembly;
                }
            }
            return null;
        }
    }
}
