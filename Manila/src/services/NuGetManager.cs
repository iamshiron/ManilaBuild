using System.IO.Compression;
using System.Reflection;
using System.Runtime.Loader;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Shiron.Manila.API.Interfaces;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Logging;
using Shiron.Manila.Profiling;

namespace Shiron.Manila.Services;

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
public class NuGetManager : INuGetManager {
    public readonly string PackageDir;
    public readonly string PackageCacheFilePath;

    private readonly Logging.ILogger _logger;
    private readonly IProfiler _profiler;

    // Caches package information. Key: "PackageID@Version", Value: Details about the package.
    private Dictionary<string, InstalledPackage> _installedPackages = [];
    // Caches resolved package dependencies to avoid redundant API calls. Key: "PackageID@Version".
    private readonly Dictionary<string, SourcePackageDependencyInfo> _resolvedPackageCache = [];
    private List<string>? _basePackages;

    private SourceRepository? _repository;
    private DependencyInfoResource? _dependencyResource;
    private FindPackageByIdResource? _findPackageByIdResource;
    private readonly NuGetFramework _currentTargetFramework;

    private readonly Task _cacheLoadTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="NuGetManager"/> class.
    /// </summary>
    /// <param name="packageDir">The directory to store downloaded packages.</param>
    public NuGetManager(Logging.ILogger logger, IProfiler profiler, string packageDir) {
        _logger = logger;
        _profiler = profiler;

        using (new ProfileScope(_profiler, "Initialize NuGetManager")) {
            PackageDir = packageDir;
            PackageCacheFilePath = Path.Combine(PackageDir, "nuget.json");
            _ = Directory.CreateDirectory(PackageDir); // Ensures the directory exists.

            _currentTargetFramework = NuGetFramework.Parse(GetCurrentFrameworkName());

            _cacheLoadTask = LoadCacheAsync();
        }
    }

    public void InitNuGetRepository() {
        if (_repository != null) return;
        using (new ProfileScope(_profiler, MethodBase.GetCurrentMethod()!)) {
            _repository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
            _dependencyResource = _repository.GetResource<DependencyInfoResource>();
            _findPackageByIdResource = _repository.GetResource<FindPackageByIdResource>();
        }
    }

    public async Task LoadCacheAsync() {
        using (new ProfileScope(_profiler, MethodBase.GetCurrentMethod()!)) {
            if (!File.Exists(PackageCacheFilePath)) return;
            try {
                using FileStream stream = new(PackageCacheFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var data = await JsonSerializer.DeserializeAsync<Dictionary<string, InstalledPackage>>(stream);
                if (data != null) {
                    _installedPackages = data;
                }
            } catch (Exception e) {
                _logger.Warning($"Unable to read NuGet package cache at '{PackageCacheFilePath}'. A new cache will be created. Reason: {e.Message}");
            }
        }
    }

    public async Task PersistCacheAsync() {
        using (new ProfileScope(_profiler, MethodBase.GetCurrentMethod()!)) {
            try {
                using (new ProfileScope(_profiler, "Awaiting cache load")) {
                    await _cacheLoadTask; // Ensure the cache is loaded before proceeding.
                }

                var serializedData = JsonSerializer.Serialize(_installedPackages, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(PackageCacheFilePath, serializedData);
            } catch (Exception e) {
                _logger.Warning("Unable to write NuGet package cache! " + e.Message);
            }
        }
    }

    /// <summary>
    /// Downloads a NuGet package and all its dependencies, returning the paths to all relevant DLLs.
    /// </summary>
    /// <param name="packageId">The ID of the package to download.</param>
    /// <param name="version">The version of the package to download.</param>
    /// <returns>A list of absolute file paths to the downloaded DLLs.</returns>
    public async Task<List<string>> DownloadPackageWithDependenciesAsync(string packageId, string version) {
        using (new ProfileScope(_profiler, MethodBase.GetCurrentMethod()!)) {
            using (new ProfileScope(_profiler, "Awaiting cache load")) {
                await _cacheLoadTask; // Ensure the cache is loaded before proceeding.
            }

            string cacheKey = $"{packageId}@{version}";
            if (_installedPackages.TryGetValue(cacheKey, out var cachedPackage)) {
                // Reconstruct absolute paths from cached relative paths.
                return [.. cachedPackage.DLLPaths.Select(p => Path.Combine(PackageDir, p))];
            }

            var allPackages = new HashSet<SourcePackageDependencyInfo>(PackageIdentityComparer.Default);

            InitNuGetRepository();
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
        using (new ProfileScope(_profiler, MethodBase.GetCurrentMethod()!)) {
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
        using (new ProfileScope(_profiler, MethodBase.GetCurrentMethod()!)) {
            var key = $"{packageId}@{version}";
            if (_resolvedPackageCache.TryGetValue(key, out var cachedInfo)) {
                return cachedInfo;
            }

            var packageIdentity = new PackageIdentity(packageId, version);
            var packageInfo = await _dependencyResource!.ResolvePackage(packageIdentity, _currentTargetFramework, new SourceCacheContext(), NullLogger.Instance, CancellationToken.None).ConfigureAwait(false);

            if (packageInfo != null) {
                _resolvedPackageCache[key] = packageInfo;
            }
            return packageInfo;
        }
    }

    private async Task<string> DownloadPackageAsync(string id, NuGetVersion version) {
        using (new ProfileScope(_profiler, MethodBase.GetCurrentMethod()!)) {
            var downloadContext = Guid.NewGuid();
            var nupkgFileName = $"{id}_{version.ToNormalizedString()}.nupkg";
            _logger.Log(new NugetManagerDownloadStartEntry(id, version.ToNormalizedString(), nupkgFileName, downloadContext));

            var downloadPath = Path.Combine(PackageDir, nupkgFileName);
            if (File.Exists(downloadPath)) return downloadPath;

            using var packageStream = new MemoryStream();
            bool success = await _findPackageByIdResource!.CopyNupkgToStreamAsync(id, version, packageStream, new SourceCacheContext(), NullLogger.Instance, CancellationToken.None).ConfigureAwait(false);

            if (!success) throw new ManilaException($"Failed to download package: {id}@{version}");

            using var fileStream = new FileStream(downloadPath, FileMode.Create, FileAccess.Write);
            packageStream.Position = 0;
            await packageStream.CopyToAsync(fileStream).ConfigureAwait(false);

            _logger.Log(new NugetManagerDownloadCompleteEntry(id, version.ToNormalizedString(), downloadContext));

            return downloadPath;
        }
    }

    private List<string> GetAssemblyPaths(string nupkgPath, string extractPath) {
        using (new ProfileScope(_profiler, MethodBase.GetCurrentMethod()!)) {
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
    }

    private static string GetCurrentFrameworkName()
        => Assembly.GetEntryAssembly()?.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName
           ?? throw new ManilaException("Could not determine the application's target framework.");

    private List<string> GetInstalledBasePackages() {
        using (new ProfileScope(_profiler, MethodBase.GetCurrentMethod()!)) {
            var basePackages = new List<string>();
            var entryAssembly = Assembly.GetEntryAssembly();
            if (entryAssembly == null) {
                _logger.Warning("Could not get entry assembly. Unable to populate installed packages, loading will be slower.");
                return basePackages;
            }

            var depsFilePath = Path.Combine(AppContext.BaseDirectory, $"{entryAssembly.GetName().Name}.deps.json");
            if (!File.Exists(depsFilePath)) {
                _logger.Warning("Could not find .deps.json file. Unable to populate installed packages, loading will be slower.");
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

            _logger.Debug($"Found {basePackages.Count} installed base packages.");
            return basePackages;
        }
    }
}

/// <summary>
/// A custom AssemblyLoadContext for loading plugin assemblies and their dependencies.
/// </summary>
public class PluginLoadContext(IProfiler profiler, string pluginPath) : AssemblyLoadContext {
    private readonly AssemblyDependencyResolver _resolver = new(pluginPath);
    private readonly Dictionary<string, string> _dependencyMap = [];

    private readonly IProfiler _profiler = profiler;

    /// <summary>
    /// Adds a NuGet dependency path to the context's internal resolution map.
    /// </summary>
    /// <param name="path">The full path to the dependency DLL.</param>
    public void AddDependency(string path) {
        using (new ProfileScope(_profiler, MethodBase.GetCurrentMethod()!)) {
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
        using (new ProfileScope(_profiler, MethodBase.GetCurrentMethod()!)) {
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
        using (new ProfileScope(_profiler, MethodBase.GetCurrentMethod()!)) {
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
        _contexts.Add(context);

        if (!_isHandlerRegistered) {
            AssemblyLoadContext.Default.Resolving += OnDefaultContextResolving;
            _isHandlerRegistered = true;
        }
    }

    /// <summary>
    /// Handles assembly resolution for the default context by checking all registered plugin contexts.
    /// This allows assemblies loaded in one plugin's context to be found by others.
    /// </summary>
    private static Assembly? OnDefaultContextResolving(AssemblyLoadContext context, AssemblyName name) {
        foreach (var pluginContext in _contexts) {
            var assembly = pluginContext.TryLoad(name);
            if (assembly != null) {
                return assembly;
            }
        }
        return null;
    }
}
