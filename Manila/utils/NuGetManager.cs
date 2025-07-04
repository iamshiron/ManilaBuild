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

public class NuGetManager {
    public readonly string PackageDir;
    private readonly Dictionary<string, List<string>> _packageCache = [];
    private static List<string>? _basePackages = null;

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
        var depsFilePath = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetEntryAssembly().GetName().Name}.deps.json");

        if (!File.Exists(depsFilePath)) {
            Logger.Warning("Could not find the .deps.json file.");
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

    public async Task<List<string>> DownloadPackageWithDependenciesAsync(string packageId, string version) {
        string cacheKey = $"{packageId}@{version}";
        if (_packageCache.ContainsKey(cacheKey)) {
            return _packageCache[cacheKey];
        }

        var repository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
        var dependencyInfoResource = await repository.GetResourceAsync<DependencyInfoResource>();
        var allPackages = new HashSet<SourcePackageDependencyInfo>(PackageIdentityComparer.Default);

        await WalkDependencyTreeAsync(packageId, new NuGetVersion(version), dependencyInfoResource, allPackages);

        var allDllPaths = new List<string>();
        foreach (var package in allPackages) {
            var nupkgPath = await DownloadPackageAsync(package.Id, package.Version.ToNormalizedString());

            // **FIXED**: Extract to a predictable, non-temporary sub-directory within PackageDir.
            var extractPath = Path.Combine(PackageDir, $"{package.Id}_{package.Version.ToNormalizedString()}");

            var packageDlls = GetAssemblyPaths(nupkgPath, extractPath);
            allDllPaths.AddRange(packageDlls);
        }

        _packageCache[cacheKey] = allDllPaths;
        return allDllPaths;
    }

    private async Task WalkDependencyTreeAsync(string packageId, NuGetVersion version, DependencyInfoResource resource, HashSet<SourcePackageDependencyInfo> collectedPackages) {
        if (_basePackages == null) PopulateInstalledPackages();
        if (packageId.StartsWith("System")) return; // Skip all .NET core packages
        if (_basePackages!.Contains(packageId)) return; // Skip all packages that already have been provided by the assembly

        var cacheContext = new SourceCacheContext();
        var currentFramework = NuGetFramework.Parse(GetCurrentFrameworkName());

        var package = await resource.ResolvePackage(new PackageIdentity(packageId, version), currentFramework, cacheContext, NullLogger.Instance, CancellationToken.None);

        if (package == null || !collectedPackages.Add(package)) {
            return;
        }

        foreach (var dependency in package.Dependencies) {
            // Use the minimal version from the allowed range.
            var dependencyVersion = dependency.VersionRange.MinVersion;
            await WalkDependencyTreeAsync(dependency.Id, dependencyVersion, resource, collectedPackages);
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
        // Only extract if the directory doesn't already exist with files in it.
        if (!Directory.Exists(extractPath) || !Directory.EnumerateFiles(extractPath, "*", SearchOption.AllDirectories).Any()) {
            ZipFile.ExtractToDirectory(nupkgPath, extractPath, true);
        }

        var libPath = Path.Combine(extractPath, "lib");
        if (!Directory.Exists(libPath)) return new List<string>();

        var currentFramework = NuGetFramework.Parse(GetCurrentFrameworkName());
        var frameworkReducer = new FrameworkReducer();

        var availableFrameworks = Directory.EnumerateDirectories(libPath)
            .Select(dir => NuGetFramework.Parse(Path.GetFileName(dir)));

        // **FIXED**: Use GetNearest to find the most compatible framework.
        var bestFramework = frameworkReducer.GetNearest(currentFramework, availableFrameworks);

        if (bestFramework == null) return new List<string>();

        // **FIXED**: Construct the path directly from the best framework's folder name.
        var bestLibDir = Path.Combine(libPath, bestFramework.GetShortFolderName());

        return Directory.EnumerateFiles(bestLibDir, "*.dll").ToList();
    }
}

public class PluginLoadContext(string pluginPath) : AssemblyLoadContext(isCollectible: false) {
    private readonly AssemblyDependencyResolver _resolver = new AssemblyDependencyResolver(pluginPath);
    // Use a dictionary for fast, accurate lookups.
    private readonly Dictionary<string, string> _dependencyMap = [];

    public void AddDependency(string path) {
        string assemblyName = Path.GetFileNameWithoutExtension(path);
        if (!_dependencyMap.ContainsKey(assemblyName)) {
            _dependencyMap.Add(assemblyName, path);
        }
    }

    public Assembly TryLoad(AssemblyName assemblyName) {
        // Check the default resolver first
        string assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath != null) {
            return LoadFromAssemblyPath(assemblyPath);
        }

        // Check the custom dependency map
        if (_dependencyMap.TryGetValue(assemblyName.Name, out string mappedPath)) {
            return LoadFromAssemblyPath(mappedPath);
        }

        return null;
    }

    // Your existing protected override Load method now just calls TryLoad
    protected override Assembly? Load(AssemblyName assemblyName) {
        if (assemblyName.Name.StartsWith("System.") || assemblyName.Name.StartsWith("Microsoft.")) {
            // Returning null tells the runtime to check the next context (the default one).
            return null;
        }

        var assembly = TryLoad(assemblyName);
        if (assembly == null) {
            return null;
        }
        return assembly;
    }
}
public static class PluginContextManager {
    private static readonly List<PluginLoadContext> _contexts = new();
    private static bool _isHandlerRegistered = false;

    public static void AddContext(PluginLoadContext context) {
        _contexts.Add(context);

        // Register the event handler only once.
        if (!_isHandlerRegistered) {
            AssemblyLoadContext.Default.Resolving += OnDefaultContextResolving;
            _isHandlerRegistered = true;
        }
    }

    private static Assembly OnDefaultContextResolving(AssemblyLoadContext context, AssemblyName name) {
        // Check every loaded plugin context for the missing assembly.
        foreach (var pluginContext in _contexts) {
            var assembly = pluginContext.TryLoad(name);
            if (assembly != null) {
                return assembly;
            }
        }
        return null;
    }
}
