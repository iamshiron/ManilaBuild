
using System.Reflection;
using System.Text.RegularExpressions;
using Shiron.Manila.Attributes;
using Shiron.Manila.Logging;
using Shiron.Manila.Profiling; // Assuming ProfileScope is in this namespace
using Shiron.Manila.Utils;

namespace Shiron.Manila.Ext;
/// <summary>
/// Manages the loading, retrieval, and lifecycle of plugins. This is a global singleton.
/// </summary>
public class ExtensionManager {
    private ExtensionManager() { }

    private static readonly ExtensionManager _instance = new();

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

    public static ExtensionManager GetInstance() {
        // This is a simple getter for a static readonly instance, profiling overhead is not beneficial here.
        return _instance;
    }

    public string? PluginDir { get; private set; }
    public List<ManilaPlugin> Plugins { get; } = [];

    /// <summary>
    /// Initializes the manager with the plugin directory.
    /// </summary>
    /// <param name="pluginDir">The directory to search for plugins. Subdirectories are not searched.</param>
    public void Init(string pluginDir) {
        // This is a simple assignment, profiling overhead is not beneficial here.
        this.PluginDir = pluginDir;
    }

    /// <summary>
    /// Discovers and loads all plugins from the specified plugin directory.
    /// </summary>
    public void LoadPlugins() {
        using (new ProfileScope(MethodBase.GetCurrentMethod()!)) {
            if (PluginDir == null) throw new Exception("Plugin directory not set. Call Init() first.");

            Logger.Log(new LoadingPluginsLogEntry(PluginDir, Guid.NewGuid()));

            if (!Directory.Exists(PluginDir)) {
                Logger.Warning($"Plugin directory does not exist: {PluginDir}. Skipping plugin loading.");
                return;
            }

            var nugetManager = ManilaEngine.GetInstance().NuGetManager;
            foreach (var file in Directory.GetFiles(PluginDir, "*.dll")) {
                using (new ProfileScope($"LoadPluginFile: {Path.GetFileName(file)}")) { // Profile each plugin file loading
                    var loadContext = new PluginLoadContext(file);
                    PluginContextManager.AddContext(loadContext);
                    var assembly = Assembly.LoadFile(Path.GetFullPath(file));

                    foreach (var type in assembly.GetTypes()) {
                        if (!type.IsSubclassOf(typeof(ManilaPlugin)) || type.IsAbstract) continue;

                        var plugin = (ManilaPlugin?) Activator.CreateInstance(type) ?? throw new Exception($"Failed to create instance of plugin {type} from {file}.");
                        plugin.File = file;
                        Plugins.Add(plugin);
                        Logger.Log(new LoadingPluginLogEntry(plugin, Guid.NewGuid()));

                        // Handle NuGet dependencies
                        foreach (var dep in plugin.NugetDependencies) {
                            using (new ProfileScope($"ResolveNugetDependency: {dep} for {plugin.Name}")) { // Profile NuGet dependency resolution
                                var match = NugetDependencyPattern.Match(dep);
                                if (!match.Success) throw new Exception($"Invalid NuGet dependency format: '{dep}' in plugin {plugin.Name}.");

                                var package = match.Groups["package"].Value;
                                var version = match.Groups["version"].Value;

                                Logger.Info($"Plugin {plugin.Name} requires dependency: {package}@{version}");
                                var nugetContextID = Guid.NewGuid();
                                Logger.Log(new NuGetPackageLoadingLogEntry(package, version, plugin, nugetContextID));

                                List<string> nugetPackages = [];
                                using (new ProfileScope("Download Dependencies")) {
                                    nugetPackages = nugetManager.DownloadPackageWithDependenciesAsync(package, version).GetAwaiter().GetResult();
                                }

                                using (new ProfileScope("Load Assemblies")) {
                                    foreach (var assemblyPath in nugetPackages) {
                                        Logger.Log(new NuGetSubPackageLoadingEntry(assemblyPath, nugetContextID));
                                        loadContext.AddDependency(assemblyPath);
                                    }
                                }
                                Logger.Info($"Resolved and registered {nugetPackages.Count} assemblies for {package}.");
                            }
                        }

                        Logger.Debug($"Loaded {plugin.GetType().FullName}!");
                        // Inject plugin instance into static properties marked with [PluginInstance]
                        foreach (var prop in type.GetProperties()) {
                            if (prop.GetCustomAttribute<PluginInstance>() != null)
                                prop.SetValue(null, plugin);
                        }
                    }
                }
            }
            nugetManager.PersistCache();
        }
    }

    /// <summary>
    /// Calls the Init() method on all loaded plugins.
    /// </summary>
    public void InitPlugins() {
        using (new ProfileScope(MethodBase.GetCurrentMethod()!)) {
            foreach (var plugin in Plugins) {
                using (new ProfileScope($"InitPlugin: {plugin.Name}")) { // Profile each plugin's Init
                    plugin.Init();
                }
            }
        }
    }

    /// <summary>
    /// Calls the Release() method on all loaded plugins.
    /// </summary>
    public void ReleasePlugins() {
        using (new ProfileScope(MethodBase.GetCurrentMethod()!)) {
            foreach (var plugin in Plugins) {
                using (new ProfileScope($"ReleasePlugin: {plugin.Name}")) { // Profile each plugin's Release
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
        using (new ProfileScope(MethodBase.GetCurrentMethod()!)) {
            return (T) GetPlugin(typeof(T));
        }
    }

    public ManilaPlugin GetPlugin(Type type) {
        using (new ProfileScope(MethodBase.GetCurrentMethod()!)) {
            foreach (var plugin in Plugins) {
                if (plugin.GetType() == type) return plugin;
            }
            throw new Exception($"Plugin of type {type} not found.");
        }
    }

    public ManilaPlugin GetPlugin(string group, string name, string? version = null) {
        using (new ProfileScope(MethodBase.GetCurrentMethod()!)) {
            if (version == string.Empty) version = null;
            foreach (var plugin in Plugins) {
                if (plugin.Group == group && plugin.Name == name && (version == null || plugin.Version == version)) {
                    return plugin;
                }
            }
            throw new Exception($"Plugin not found: {group}:{name}{(version == null ? "" : "@" + version)}");
        }
    }

    public PluginComponent GetPluginComponent(string group, string name, string component, string? version = null) {
        using (new ProfileScope(MethodBase.GetCurrentMethod()!)) {
            // This calls GetPlugin, which is already profiled.
            // The GetComponent call itself is internal to ManilaPlugin and its complexity is managed there.
            return GetPlugin(group, name, version).GetComponent(component);
        }
    }

    /// <summary>
    /// Gets a plugin by its string key.
    /// </summary>
    /// <param name="key">The key, compliant to the format specified in <see cref="PluginPattern"/>.</param>
    /// <returns>The plugin instance.</returns>
    /// <exception cref="Exception">Thrown if the key is invalid or plugin is not found.</exception>
    public ManilaPlugin GetPlugin(string key) {
        using (new ProfileScope(MethodBase.GetCurrentMethod()!)) {
            var match = PluginPattern.Match(key);
            if (!match.Success) throw new Exception("Invalid plugin key: " + key);
            // This calls another GetPlugin overload, which is already profiled.
            return GetPlugin(match.Groups["group"].Value, match.Groups["name"].Value, match.Groups["version"].Value);
        }
    }

    /// <summary>
    /// Gets a plugin component by its string key.
    /// </summary>
    /// <param name="key">The key, compliant to the format specified in <see cref="ComponentPattern"/>.</param>
    /// <returns>The component instance.</returns>
    /// <exception cref="Exception">Thrown if the key is invalid or component is not found.</exception>
    public PluginComponent GetPluginComponent(string key) {
        using (new ProfileScope(MethodBase.GetCurrentMethod()!)) {
            var match = ComponentPattern.Match(key);
            if (!match.Success) throw new Exception("Invalid component key: " + key);
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
        using (new ProfileScope(MethodBase.GetCurrentMethod()!)) {
            var match = APIClassPattern.Match(key);
            if (!match.Success) throw new Exception("Invalid API class key: " + key);
            // This calls GetPlugin, which is already profiled.
            return GetPlugin(match.Groups["group"].Value, match.Groups["name"].Value, match.Groups["version"].Value).GetAPIClass(match.Groups["class"].Value);
        }
    }
}
