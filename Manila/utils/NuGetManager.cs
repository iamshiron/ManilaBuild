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

namespace Shiron.Manila.Utils;

/// <summary>
/// Manages downloading, caching, and resolving NuGet packages and their dependencies.
/// </summary>
public class NuGetManager {
    /// <summary>
    /// The directory where NuGet packages are stored.
    /// </summary>
    public readonly string PackageDir;
    private readonly Dictionary<string, List<string>> _packageCache = [];
    private static List<string>? _basePackages = null;

    /// <summary>
    /// Initializes a new instance of the <see cref="NuGetManager"/> class.
    /// </summary>
    /// <param name="packageDir">The directory to store downloaded packages.</param>
    public NuGetManager(string packageDir) {
        PackageDir = packageDir;
        if (!Directory.Exists(PackageDir)) Directory.CreateDirectory(PackageDir);
    }

    private string GetCurrentFrameworkName() {
        return Assembly.GetEntryAssembly()?
                       .GetCustomAttribute<TargetFrameworkAttribute>()?
                       .FrameworkName
               ?? throw new InvalidOperationException("Could not determine target framework.");
    }

    private void PopulateInstalledPackages() {
        _basePackages = [];
        var entryAssembly = Assembly.GetEntryAssembly();
        if (entryAssembly == null) {
            Logger.Warning("Could not get entry assembly.");
            return;
        }
        var depsFilePath = Path.Combine(AppContext.BaseDirectory, $"{entryAssembly.GetName().Name}.deps.json");

        if (!File.Exists(depsFilePath)) {
            Logger.Warning("Could not find the .deps.json file.");
            return;
        }

        var fileContent = File.ReadAllText(depsFilePath);
        using var jsonDocument = JsonDocument.Parse(fileContent);
        var root = jsonDocument.RootElement;

        if (root.TryGetProperty("libraries", out var libraries)) {
            foreach (var property in libraries.EnumerateObject()) {
                var libraryName = property.Name;
                if (property.Value.TryGetProperty("type", out var type) && type.GetString() == "package") {
                    var packageName = libraryName.Split('/')[0];
                    _basePackages.Add(packageName);
                }
            }
        }

        Logger.Debug($"Found {_basePackages.Count} installed base packages!");
    }

    /// <summary>
    /// Downloads a NuGet package and all its dependencies, returning the paths to all relevant DLLs.
    /// </summary>
    /// <param name="packageId">The ID of the package to download.</param>
    /// <param name="version">The version of the package to download.</param>
    /// <returns>A list of file paths to the downloaded DLLs.</returns>
    public async Task<List<string>> DownloadPackageWithDependenciesAsync(string packageId, string version) {
        string cacheKey = $"{packageId}@{version}";
        if (_packageCache.TryGetValue(cacheKey, out var cachedPaths)) {
            return cachedPaths;
        }

        var repository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
        var dependencyInfoResource = await repository.GetResourceAsync<DependencyInfoResource>();
        var allPackages = new HashSet<SourcePackageDependencyInfo>(PackageIdentityComparer.Default);

        await WalkDependencyTreeAsync(packageId, new NuGetVersion(version), dependencyInfoResource, allPackages);

        var allDllPaths = new List<string>();
        foreach (var package in allPackages) {
            var nupkgPath = await DownloadPackageAsync(package.Id, package.Version.ToNormalizedString());
            var extractPath = Path.Combine(PackageDir, $"{package.Id}_{package.Version.ToNormalizedString()}");
            var packageDlls = GetAssemblyPaths(nupkgPath, extractPath);
            allDllPaths.AddRange(packageDlls);
        }

        _packageCache[cacheKey] = allDllPaths;
        return allDllPaths;
    }

    private async Task WalkDependencyTreeAsync(string packageId, NuGetVersion version, DependencyInfoResource resource, HashSet<SourcePackageDependencyInfo> collectedPackages) {
        if (_basePackages == null) PopulateInstalledPackages();

        // Skip system packages or packages already provided by the host application.
        if (packageId.StartsWith("System") || _basePackages!.Contains(packageId)) return;

        var cacheContext = new SourceCacheContext();
        var currentFramework = NuGetFramework.Parse(GetCurrentFrameworkName());
        var package = await resource.ResolvePackage(new PackageIdentity(packageId, version), currentFramework, cacheContext, NullLogger.Instance, CancellationToken.None);

        if (package == null || !collectedPackages.Add(package)) {
            return;
        }

        foreach (var dependency in package.Dependencies) {
            var dependencyVersion = dependency.VersionRange.MinVersion;
            await WalkDependencyTreeAsync(dependency.Id, dependencyVersion!, resource, collectedPackages);
        }
    }

    private async Task<string> DownloadPackageAsync(string id, string version) {
        var downloadPath = Path.Combine(PackageDir, $"{id}_{version}.nupkg");
        if (File.Exists(downloadPath)) return downloadPath;

        var repository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
        var resource = await repository.GetResourceAsync<FindPackageByIdResource>();
        var packageVersion = new NuGetVersion(version);
        var cacheContext = new SourceCacheContext();

        using var packageStream = new MemoryStream();
        bool success = await resource.CopyNupkgToStreamAsync(id, packageVersion, packageStream, cacheContext, NullLogger.Instance, CancellationToken.None);

        if (!success) throw new Exception($"Failed to download package: {id}@{version}");

        using var fileStream = new FileStream(downloadPath, FileMode.Create);
        packageStream.Position = 0;
        await packageStream.CopyToAsync(fileStream);

        return downloadPath;
    }

    private List<string> GetAssemblyPaths(string nupkgPath, string extractPath) {
        if (!Directory.Exists(extractPath) || !Directory.EnumerateFiles(extractPath, "*", SearchOption.AllDirectories).Any()) {
            ZipFile.ExtractToDirectory(nupkgPath, extractPath, true);
        }

        var libPath = Path.Combine(extractPath, "lib");
        if (!Directory.Exists(libPath)) return [];

        var currentFramework = NuGetFramework.Parse(GetCurrentFrameworkName());
        var frameworkReducer = new FrameworkReducer();

        var availableFrameworks = Directory.EnumerateDirectories(libPath)
            .Select(dir => NuGetFramework.Parse(Path.GetFileName(dir)));

        var bestFramework = frameworkReducer.GetNearest(currentFramework, availableFrameworks);
        if (bestFramework == null) return [];

        var bestLibDir = Path.Combine(libPath, bestFramework.GetShortFolderName());
        return Directory.EnumerateFiles(bestLibDir, "*.dll").ToList();
    }
}

/// <summary>
/// A custom AssemblyLoadContext for loading plugin assemblies and their dependencies.
/// </summary>
public class PluginLoadContext(string pluginPath) : AssemblyLoadContext(isCollectible: false) {
    private readonly AssemblyDependencyResolver _resolver = new(pluginPath);
    private readonly Dictionary<string, string> _dependencyMap = [];

    /// <summary>
    /// Adds a dependency path to the context's internal map.
    /// </summary>
    /// <param name="path">The full path to the dependency DLL.</param>
    public void AddDependency(string path) {
        string assemblyName = Path.GetFileNameWithoutExtension(path);
        _dependencyMap.TryAdd(assemblyName, path);
    }

    /// <summary>
    /// Tries to load an assembly by its name, checking the default resolver and then the custom dependency map.
    /// </summary>
    /// <param name="assemblyName">The name of the assembly to load.</param>
    /// <returns>The loaded Assembly, or null if it could not be found.</returns>
    public Assembly? TryLoad(AssemblyName assemblyName) {
        string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath != null) {
            return LoadFromAssemblyPath(assemblyPath);
        }

        if (assemblyName.Name != null && _dependencyMap.TryGetValue(assemblyName.Name, out string? mappedPath) && mappedPath != null) {
            return LoadFromAssemblyPath(mappedPath);
        }

        return null;
    }

    protected override Assembly? Load(AssemblyName assemblyName) {
        // Defer system assemblies to the default AssemblyLoadContext.
        if (assemblyName.Name != null && (assemblyName.Name.StartsWith("System.") || assemblyName.Name.StartsWith("Microsoft."))) {
            return null;
        }

        return TryLoad(assemblyName);
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
