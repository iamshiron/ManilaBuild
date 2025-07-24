
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
    ManilaPlugin GetPlugin(string group, string name, string? version = null);
    ManilaPlugin GetPlugin(string key);
    PluginComponent GetPluginComponent(string group, string name, string component, string? version = null);
    PluginComponent GetPluginComponent(string key);
    Type GetAPIType(string key);

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
            ?? throw new ManilaException($"Failed to create instance of plugin {pluginType} from {file}.");

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

    /// <summary>
    /// Gets a loaded plugin by its type.
    /// </summary>
    /// <typeparam name="T">The type of the plugin to find.</typeparam>
    /// <returns>The plugin instance.</returns>
    /// <exception cref="Exception">Thrown if the plugin is not found.</exception>
    public T GetPlugin<T>() where T : ManilaPlugin {
        using (new ProfileScope(_profiler, MethodBase.GetCurrentMethod()!)) {
            return (T) GetPlugin(typeof(T));
        }
    }

    public ManilaPlugin GetPlugin(Type type) {
        using (new ProfileScope(_profiler, MethodBase.GetCurrentMethod()!)) {
            return Plugins.FirstOrDefault(plugin => plugin.GetType() == type) ??
                throw new ManilaException($"Plugin of type {type} not found.");
        }
    }

    public ManilaPlugin GetPlugin(string group, string name, string? version = null) {
        using (new ProfileScope(_profiler, MethodBase.GetCurrentMethod()!)) {
            if (version == string.Empty) version = null;
            foreach (var plugin in Plugins) {
                if (plugin.Group == group && plugin.Name == name && (version == null || plugin.Version == version)) {
                    return plugin;
                }
            }
            throw new ManilaException($"Plugin not found: {group}:{name}{(version == null ? "" : "@" + version)}");
        }
    }

    /// <summary>
    /// Gets a plugin by its string key.
    /// </summary>
    /// <param name="key">The key, compliant to the format specified in <see cref="PluginPattern"/>.</param>
    /// <returns>The plugin instance.</returns>
    /// <exception cref="Exception">Thrown if the key is invalid or plugin is not found.</exception>
    public ManilaPlugin GetPlugin(string key) {
        using (new ProfileScope(_profiler, MethodBase.GetCurrentMethod()!)) {
            var match = PluginPattern.Match(key);
            if (!match.Success) throw new ManilaException("Invalid plugin key: " + key);
            // This calls another GetPlugin overload, which is already profiled.
            return GetPlugin(match.Groups["group"].Value, match.Groups["name"].Value, match.Groups["version"].Value);
        }
    }

    public PluginComponent GetPluginComponent(string group, string name, string component, string? version = null) {
        using (new ProfileScope(_profiler, MethodBase.GetCurrentMethod()!)) {
            // This calls GetPlugin, which is already profiled.
            // The GetComponent call itself is internal to ManilaPlugin and its complexity is managed there.
            return GetPlugin(group, name, version).GetComponent(component);
        }
    }

    /// <summary>
    /// Gets a plugin component by its string key.
    /// </summary>
    /// <param name="key">The key, compliant to the format specified in <see cref="ComponentPattern"/>.</param>
    /// <returns>The component instance.</returns>
    /// <exception cref="Exception">Thrown if the key is invalid or component is not found.</exception>
    public PluginComponent GetPluginComponent(string key) {
        using (new ProfileScope(_profiler, MethodBase.GetCurrentMethod()!)) {
            var match = ComponentPattern.Match(key);
            if (!match.Success) throw new ManilaException("Invalid component key: " + key);
            // This calls GetPluginComponent, which is already profiled.
            return GetPluginComponent(match.Groups["group"].Value, match.Groups["name"].Value, match.Groups["component"].Value, match.Groups["version"].Value);
        }
    }

    /// <summary>
    /// Gets a plugin's exported API type by its string key.
    /// </summary>
    /// <param name="key">The key, compliant to the format specified in <see cref="APIClassPattern"/>.</param>
    /// <returns>The API type.</returns>
    /// <exception cref="Exception">Thrown if the key is invalid or the class is not found.</exception>
    public Type GetAPIType(string key) {
        using (new ProfileScope(_profiler, MethodBase.GetCurrentMethod()!)) {
            var match = APIClassPattern.Match(key);
            if (!match.Success) throw new ManilaException("Invalid API class key: " + key);
            // This calls GetPlugin, which is already profiled.
            return GetPlugin(match.Groups["group"].Value, match.Groups["name"].Value, match.Groups["version"].Value).GetAPIClass(match.Groups["class"].Value);
        }
    }
}
