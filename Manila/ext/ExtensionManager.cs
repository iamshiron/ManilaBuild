using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using System.Text.RegularExpressions;
using Shiron.Manila.Attributes;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Logging;
using Shiron.Manila.Profiling;
using Shiron.Manila.Utils;

namespace Shiron.Manila.Ext;

public interface IExtensionManager {
    Task LoadPluginsAsync();
    void InitPlugins();
    void ReleasePlugins();
    T GetPlugin<T>() where T : ManilaPlugin;

    ManilaPlugin GetPlugin(Type type);
    ManilaPlugin GetPlugin(string uri);
    ManilaPlugin GetPlugin(RegexUtils.PluginMatch match);

    PluginComponent GetPluginComponent(string uri);
    PluginComponent GetPluginComponent(RegexUtils.PluginComponentMatch match);

    Type GetAPIType(string uri);
    Type GetAPIType(RegexUtils.PluginApiClassMatch match);

    public List<ManilaPlugin> Plugins { get; }
}

/// <summary>
/// Manages the loading, retrieval, and lifecycle of plugins. This is a global singleton.
/// </summary>
public class ExtensionManager(ILogger logger, IProfiler profiler, string _pluginDir, INuGetManager nuGetManager) : IExtensionManager {
    private readonly ILogger _logger = logger;
    private readonly IProfiler _profiler = profiler;
    private readonly string _pluginDir = _pluginDir;

    private readonly INuGetManager _nuGetManager = nuGetManager;

    /// <summary>
    /// The default group assigned to plugins that do not specify one.
    /// </summary>
    public static readonly string DEFAULT_GROUP = "shiron.manila";

    /// <summary>
    /// Matches plugin keys in the format: "group:name@version". Version is optional.
    /// </summary>
    public static readonly Regex PluginPattern = new(@"(?<group>[\w.\d]+):(?<name>[\w.\d]+)(?:@(?<version>[\w.\d]+))?", RegexOptions.Compiled);

    /// <summary>
    /// Matches component keys in the format: "group:name@version:component". Version is optional.
    /// </summary>
    public static readonly Regex ComponentPattern = new(@"(?<group>[\w.\d]+):(?<name>[\w.\d]+)(?:@(?<version>[\w.\d]+))?:(?<component>[\w.\d]+)", RegexOptions.Compiled);

    /// <summary>
    /// Matches API class keys in the format: "group:name@version/class". Version is optional.
    /// </summary>
    public static readonly Regex APIClassPattern = new(@"(?<group>[\w.\d]+):(?<name>[\w.\d]+)(?:@(?<version>[\w.\d]+))?/(?<class>[\w.\d]+)", RegexOptions.Compiled);

    /// <summary>
    /// Matches NuGet dependencies in the format: "Package.Name@1.2.3".
    /// </summary>
    public static readonly Regex NugetDependencyPattern = new(@"(?<package>[\w.\d]+)@(?<version>[\w.\d-]+)", RegexOptions.Compiled);
    public List<ManilaPlugin> Plugins { get; } = [];

    /// <summary>
    /// Discovers and loads all plugins from the specified plugin directory.
    /// </summary>
    public async Task LoadPluginsAsync() {
        using (new ProfileScope(_profiler, MethodBase.GetCurrentMethod()!)) {
            _logger.Log(new LoadingPluginsLogEntry(_pluginDir, Guid.NewGuid()));

            if (!Directory.Exists(_pluginDir)) {
                _logger.Warning($"Plugin directory does not exist: {_pluginDir}. Skipping plugin loading.");
                return;
            }

            foreach (var file in Directory.GetFiles(_pluginDir, "*.dll")) {
                using (new ProfileScope(_profiler, $"LoadPluginFile: {Path.GetFileName(file)}")) {
                    await LoadPluginFileAsync(file);
                }
            }

            await _nuGetManager.PersistCacheAsync();
        }
    }

    private async Task LoadPluginFileAsync(string file) {
        var loadContext = new PluginLoadContext(_profiler, file);
        PluginContextManager.AddContext(loadContext);
        var assembly = Assembly.LoadFile(Path.GetFullPath(file));

        foreach (var type in assembly.GetTypes()) {
            if (!type.IsSubclassOf(typeof(ManilaPlugin)) || type.IsAbstract) continue;
            await LoadPluginAsync(type, file, loadContext);
        }
    }

    private async Task LoadPluginAsync(Type pluginType, string file, PluginLoadContext loadContext) {
        var plugin = (ManilaPlugin?) Activator.CreateInstance(pluginType)
            ?? throw new ManilaException($"Failed to create instance of plugin type {pluginType.FullName} from file {file}.");

        plugin.SetLogger(_logger);
        plugin.File = file;
        Plugins.Add(plugin);
        _logger.Log(new LoadingPluginLogEntry(plugin, Guid.NewGuid()));

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

        _logger.Info($"Plugin {plugin.Name} requires dependency: {package}@{version}");
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

        _logger.Info($"Resolved and registered {nugetPackages.Count} assemblies for {package}.");
    }

    private static void InjectPluginInstance(ManilaPlugin plugin, Type pluginType) {
        pluginType.GetProperties()
            .Where(prop => prop.GetCustomAttribute<PluginInstance>() != null)
            .ToList()
            .ForEach(prop => prop.SetValue(null, plugin));
    }

    /// <summary>
    /// Calls the Init() method on all loaded plugins.
    /// </summary>
    public void InitPlugins() {
        using (new ProfileScope(_profiler, MethodBase.GetCurrentMethod()!)) {
            foreach (var plugin in Plugins) {
                using (new ProfileScope(_profiler, $"InitPlugin: {plugin.Name}")) { // Profile each plugin's Init
                    plugin.Init();
                }
            }
        }
    }

    /// <summary>
    /// Calls the Release() method on all loaded plugins.
    /// </summary>
    public void ReleasePlugins() {
        using (new ProfileScope(_profiler, MethodBase.GetCurrentMethod()!)) {
            foreach (var plugin in Plugins) {
                using (new ProfileScope(_profiler, $"ReleasePlugin: {plugin.Name}")) { // Profile each plugin's Release
                    plugin.Release();
                }
            }
        }
    }

    public T GetPlugin<T>() where T : ManilaPlugin {
        var plugin = Plugins.FirstOrDefault(p => p is T);
        return plugin is T typedPlugin ? typedPlugin : throw new ManilaException($"Plugin of type {typeof(T).FullName} not found.");
    }
    public ManilaPlugin GetPlugin(Type type) {
        var plugin = Plugins.FirstOrDefault(p => p.GetType() == type);
        return plugin ?? throw new ManilaException($"Plugin of type {type.FullName} not found.");
    }

    public ManilaPlugin GetPlugin(string uri) {
        return GetPlugin(RegexUtils.MatchPlugin(uri) ?? throw new ManilaException(uri));
    }
    public ManilaPlugin GetPlugin(RegexUtils.PluginMatch match) {
        var plugin = Plugins.FirstOrDefault(p =>
            p.Group == match.Group && p.Name == match.Plugin && (p.Version == match.Version || match.Version == null)
        );
        return plugin ?? throw new ManilaException($"Plugin not found for match: {match}");
    }

    public PluginComponent GetPluginComponent(string uri) {
        return GetPluginComponent(RegexUtils.MatchPluginComponent(uri) ?? throw new ManilaException(uri));
    }
    public PluginComponent GetPluginComponent(RegexUtils.PluginComponentMatch match) {
        var plugin = GetPlugin(match.ToPluginMatch());
        var component = plugin.Components.FirstOrDefault(c =>
            c.Value.Name == match.Component
        );

        return component.Value ?? throw new ManilaException($"Component '{match.Component}' not found in plugin '{plugin.Name}' with match: {match}");
    }

    public Type GetAPIType(string uri) {
        return GetAPIType(RegexUtils.MatchPluginApiClass(uri) ?? throw new ManilaException(uri));
    }
    public Type GetAPIType(RegexUtils.PluginApiClassMatch match) {
        var plugin = GetPlugin(match.ToPluginMatch());
        var apiType = plugin.APIClasses.FirstOrDefault(a =>
            a.Key == match.ApiClass
        );

        return apiType.Value ?? throw new ManilaException($"API class '{match.ApiClass}' not found in plugin '{plugin.Name}' with match: {match}");
    }
}
