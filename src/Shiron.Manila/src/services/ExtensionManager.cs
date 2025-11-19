using System.Reflection;
using System.Text.RegularExpressions;
using Shiron.Logging;
using Shiron.Manila.API.Attributes;
using Shiron.Manila.API.Ext;
using Shiron.Manila.API.Interfaces;
using Shiron.Manila.API.Interfaces.Artifacts;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Logging;
using Shiron.Profiling;
using Shiron.Utils;

namespace Shiron.Manila.Services;

/// <summary>Plugin loader and registry (singleton).</summary>
public class ExtensionManager(ILogger logger, IProfiler profiler, string _pluginDir, INuGetManager nuGetManager) : IExtensionManager {
    private readonly ILogger _logger = logger;
    private readonly IProfiler _profiler = profiler;
    private readonly string _pluginDir = _pluginDir;

    private readonly INuGetManager _nuGetManager = nuGetManager;

    /// <summary>Default plugin group.</summary>
    public static readonly string DEFAULT_GROUP = "shiron.manila";

    /// <summary>Regex: group:name@version.</summary>
    public static readonly Regex PluginPattern = new(@"(?<group>[\w.\d]+):(?<name>[\w.\d]+)(?:@(?<version>[\w.\d]+))?", RegexOptions.Compiled);

    /// <summary>Regex: group:name@version:component.</summary>
    public static readonly Regex ComponentPattern = new(@"(?<group>[\w.\d]+):(?<name>[\w.\d]+)(?:@(?<version>[\w.\d]+))?:(?<component>[\w.\d]+)", RegexOptions.Compiled);

    /// <summary>Regex: group:name@version/class.</summary>
    public static readonly Regex APIClassPattern = new(@"(?<group>[\w.\d]+):(?<name>[\w.\d]+)(?:@(?<version>[\w.\d]+))?/(?<class>[\w.\d]+)", RegexOptions.Compiled);

    /// <summary>Regex: Package.Name@1.2.3.</summary>
    public static readonly Regex NugetDependencyPattern = new(@"(?<package>[\w.\d]+)@(?<version>[\w.\d-]+)", RegexOptions.Compiled);
    public List<ManilaPlugin> Plugins { get; } = [];
    public List<Assembly> Assemblies { get; } = [];

    /// <summary>Types annotated with <see cref="ManilaExpose"/>.</summary>
    public List<Type> ExposedTypes { get; } = [];

    private readonly string[] _knownAssemblies = [
        "Shiron.Manila.API.dll",
        "Shiron.Logging.dll",
        "Shiron.Profiling.dll",
        "Shiron.Utils.dll"
    ];

    /// <summary>Load all plugin assemblies in directory.</summary>
    public async Task LoadPluginsAsync() {
        using (new ProfileScope(_profiler, MethodBase.GetCurrentMethod()!)) {
            _logger.Log(new LoadingPluginsLogEntry(_pluginDir, Guid.NewGuid()));

            if (!Directory.Exists(_pluginDir)) {
                _logger.Warning($"Plugin directory does not exist: {_pluginDir}. Skipping plugin loading.");
                return;
            }

            foreach (var file in Directory.GetFiles(_pluginDir, "*.dll")) {
                if (_knownAssemblies.Contains(Path.GetFileName(file))) continue; // Skip known assemblies
                using (new ProfileScope(_profiler, $"LoadPluginFile: {Path.GetFileName(file)}")) {
                    await LoadPluginFileAsync(file);
                }
            }

            await _nuGetManager.PersistCacheAsync();
        }
    }

    private async Task LoadPluginFileAsync(string file) {
        _logger.Debug($"Loading plugin from file: {file}");

        var loadContext = new PluginLoadContext(_profiler, file);
        PluginContextManager.AddContext(loadContext);
        var assembly = loadContext.LoadFromAssemblyPath(Path.GetFullPath(file));

        foreach (var type in assembly.GetTypes()) {
            if (!type.IsSubclassOf(typeof(ManilaPlugin)) || type.IsAbstract) continue;
            _logger.Debug($"Found plugin type: {type.FullName} in assembly {assembly.GetName().Name}");
            await LoadPluginAsync(type, assembly, file, loadContext);
            return;
        }

        _logger.Warning($"No valid ManilaPlugin found in assembly {assembly.GetName().Name} from file {file}. Skipping.");
    }

    private async Task LoadPluginAsync(Type pluginType, Assembly assembly, string file, PluginLoadContext loadContext) {
        var plugin = (ManilaPlugin?) Activator.CreateInstance(pluginType)
            ?? throw new ManilaException($"Failed to create instance of plugin type {pluginType.FullName} from file {file}.");

        plugin.SetLogger(_logger);
        plugin.File = file;
        Assemblies.Add(assembly);
        Plugins.Add(plugin);
        _logger.Log(new LoadingPluginLogEntry(plugin, Guid.NewGuid()));

        foreach (var t in assembly.GetTypes()) {
            if (t.GetCustomAttribute<ManilaExpose>() != null) {
                ExposedTypes.Add(t);
            }
        }

        await ResolveDependenciesAsync(plugin, loadContext);

        _logger.Debug($"Loaded {plugin.GetType().FullName}!");
        InjectPluginInstance(plugin, pluginType);
    }

    private async Task ResolveDependenciesAsync(ManilaPlugin plugin, PluginLoadContext loadContext) {
        foreach (var dep in plugin.NugetDependencies) {
            using (new ProfileScope(_profiler, $"ResolveNugetDependency: {dep} for {plugin.Name}")) {
                await ResolveNuGetDependencyAsync(dep, plugin, loadContext);
            }
        }
    }

    private async Task ResolveNuGetDependencyAsync(string dep, ManilaPlugin plugin, PluginLoadContext loadContext) {
        var match = NugetDependencyPattern.Match(dep);
        if (!match.Success)
            throw new ManilaException($"Invalid NuGet dependency format: '{dep}' in plugin {plugin.Name}.");

        var package = match.Groups["package"].Value;
        var version = match.Groups["version"].Value;

        _logger.Debug($"Plugin {plugin.Name} requires dependency: {package}@{version}");
        var nugetContextID = Guid.NewGuid();
        _logger.Log(new NuGetPackageLoadingLogEntry(package, version, plugin, nugetContextID));

        List<string> nugetPackages;
        using (new ProfileScope(_profiler, "Download Dependencies")) {
            nugetPackages = await _nuGetManager.DownloadPackageWithDependenciesAsync(package, version);
        }

        using (new ProfileScope(_profiler, "Load Assemblies")) {
            foreach (var assemblyPath in nugetPackages) {
                _logger.Log(new NuGetSubPackageLoadingEntry(assemblyPath, nugetContextID));
                loadContext.AddDependency(assemblyPath);
            }
        }

        _logger.Debug($"Resolved and registered {nugetPackages.Count} assemblies for {package}.");
    }

    private static void InjectPluginInstance(ManilaPlugin plugin, Type pluginType) {
        pluginType.GetProperties()
            .Where(prop => prop.GetCustomAttribute<PluginInstance>() != null)
            .ToList()
            .ForEach(prop => prop.SetValue(null, plugin));
    }

    /// <summary>Invoke Init() on each plugin.</summary>
    public void InitPlugins() {
        using (new ProfileScope(_profiler, MethodBase.GetCurrentMethod()!)) {
            foreach (var plugin in Plugins) {
                using (new ProfileScope(_profiler, $"InitPlugin: {plugin.Name}")) { // Profile each plugin's Init
                    plugin.Init();

                    foreach (var dep in plugin.Dependencies) {
                        var parseFunc = dep.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static) ?? throw new ManilaException($"Dependency type '{dep.FullName}' does not have a static Parse method.");
                        _logger.Debug($"Registering script lambda for dependency type: {dep.FullName} in plugin {plugin.Name} as '{dep.Name}'");
                        API.Manila.DependencyLambdas[dep.Name] = (args) => {
                            return parseFunc.Invoke(null, new object?[] { args }) ?? throw new ManilaException($"Parse method for dependency '{dep.Name}' returned null.");
                        };
                    }
                }
            }
        }
    }

    /// <summary>Invoke Release() on each plugin.</summary>
    public void ReleasePlugins() {
        using (new ProfileScope(_profiler, MethodBase.GetCurrentMethod()!)) {
            foreach (var plugin in Plugins) {
                using (new ProfileScope(_profiler, $"ReleasePlugin: {plugin.Name}")) { // Profile each plugin's Release
                    plugin.Release();
                }
            }
        }
    }

    /// <summary>Get plugin by type.</summary>
    /// <typeparam name="T">Plugin subclass.</typeparam>
    /// <returns>Plugin instance.</returns>
    public T GetPlugin<T>() where T : ManilaPlugin {
        var plugin = Plugins.FirstOrDefault(p => p is T);
        return plugin is T typedPlugin ? typedPlugin : throw new ManilaException($"Plugin of type {typeof(T).FullName} not found.");
    }
    /// <summary>Get plugin by runtime <see cref="Type"/>.</summary>
    public ManilaPlugin GetPlugin(Type type) {
        var plugin = Plugins.FirstOrDefault(p => p.GetType() == type);
        return plugin ?? throw new ManilaException($"Plugin of type {type.FullName} not found.");
    }

    /// <summary>Get plugin via URI pattern.</summary>
    public ManilaPlugin GetPlugin(string uri) {
        return GetPlugin(RegexUtils.MatchPlugin(uri) ?? throw new ManilaException(uri));
    }
    /// <summary>Get plugin via parsed match.</summary>
    public ManilaPlugin GetPlugin(RegexUtils.PluginMatch match) {
        var plugin = Plugins.FirstOrDefault(p =>
            p.Group == match.Group && p.Name == match.Plugin && (p.Version == match.Version || match.Version == null)
        );
        return plugin ?? throw new ManilaException($"Plugin not found for match: {match}");
    }

    /// <summary>Get component via URI.</summary>
    public PluginComponent GetPluginComponent(string uri) {
        return GetPluginComponent(RegexUtils.MatchPluginComponent(uri) ?? throw new ManilaException(uri));
    }
    /// <summary>Get component via parsed match.</summary>
    public PluginComponent GetPluginComponent(RegexUtils.PluginComponentMatch match) {
        var plugin = GetPlugin(match.ToPluginMatch());
        var component = plugin.Components.FirstOrDefault(c =>
            c.Value.Name == match.Component
        );

        return component.Value ?? throw new ManilaException($"Component '{match.Component}' not found in plugin '{plugin.Name}' with match: {match}");
    }

    /// <summary>Get artifact blueprint via URI.</summary>
    public IArtifactBlueprint GetArtifact(string uri) => GetArtifact(
        RegexUtils.MatchPluginComponent(uri) ?? throw new ManilaException(uri)
    );
    /// <summary>Get artifact blueprint via match.</summary>
    public IArtifactBlueprint GetArtifact(RegexUtils.PluginComponentMatch match) {
        var plugin = GetPlugin(match.ToPluginMatch());
        var builder = plugin.ArtifactBuilderTypes.FirstOrDefault(b =>
            b.Item1 == match.Component
        );

        if (builder == default) {
            throw new ManilaException($"Artifact builder '{match.Component}' not found in plugin '{plugin.Name}' with match: {match}");
        }

        var instance = Activator.CreateInstance(builder.Item2);
        return instance is IArtifactBlueprint blueprint ? blueprint : throw new ManilaException($"Failed to create instance of artifact builder '{match.Component}' in plugin '{plugin.Name}' with match: {match}");
    }

    /// <summary>Get exposed API type via URI.</summary>
    public Type GetAPIType(string uri) {
        return GetAPIType(RegexUtils.MatchPluginApiClass(uri) ?? throw new ManilaException(uri));
    }
    /// <summary>Get exposed API type via match.</summary>
    public Type GetAPIType(RegexUtils.PluginApiClassMatch match) {
        var plugin = GetPlugin(match.ToPluginMatch());
        var apiType = plugin.APIClasses.FirstOrDefault(a =>
            a.Key == match.ApiClass
        );

        return apiType.Value ?? throw new ManilaException($"API class '{match.ApiClass}' not found in plugin '{plugin.Name}' with match: {match}");
    }
}
