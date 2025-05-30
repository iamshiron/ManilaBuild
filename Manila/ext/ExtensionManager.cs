namespace Shiron.Manila.Ext;

using System.Reflection;
using System.Text.RegularExpressions;
using Shiron.Manila.Utils;
using Shiron.Manila.Attributes;

/// <summary>
/// Class for loading and managing plugins. Global singleton.
/// </summary>
public class ExtensionManager {
    /// <summary>
    /// Private constructor to prevent instantiation.
    /// </summary>
    private ExtensionManager() { }

    /// <summary>
    /// Singleton instance of the extension manager.
    /// </summary>
    private static readonly ExtensionManager _instance = new();
    /// <summary>
    /// Default group for plugins. Used when no group is specified.
    /// </summary>
    public static readonly string DEFAULT_GROUP = "shiron.manila";

    /// <summary>
    /// Regular expression pattern for matching plugin keys.
    /// </summary>
    public static readonly Regex pluginPattern = new(@"(?<group>[\w.\d]+):(?<name>[\w.\d]+)(?:@(?<version>[\w.\d]+))?", RegexOptions.Compiled);
    /// <summary>
    /// Regular expression pattern for matching component keys inside of plugins.
    /// </summary>
    public static readonly Regex componentPattern = new(@"(?<group>[\w.\d]+):(?<name>[\w.\d]+)(?:@(?<version>[\w.\d]+))?:(?<component>[\w.\d]+)", RegexOptions.Compiled);
    /// <summary>
    /// Regular expression pattern for matching API classes inside of plugins.
    /// </summary>
    public static readonly Regex apiClassPattern = new(@"(?<group>[\w.\d]+):(?<name>[\w.\d]+)(?:@(?<version>[\w.\d]+))?/(?<class>[\w.\d]+)", RegexOptions.Compiled);
    /// <summary>
    /// Regular expression pattern for matching NuGet dependencies.
    /// </summary>
    public static readonly Regex nugetDependencyPattern = new(@"(?<package>[\w.\d]+)@(?<version>[\w.\d]+)", RegexOptions.Compiled);

    /// <summary>
    /// Returns the singleton instance of the extension manager.
    /// </summary>
    /// <returns></returns>
    public static ExtensionManager GetInstance() {
        if (_instance == null) throw new Exception("Extension manager not initialized");
        return _instance;
    }

    /// <summary>
    /// The directory where plugins are located.
    /// </summary>
    public string? PluginDir { get; private set; }
    /// <summary>
    /// List of loaded plugins.
    /// </summary>
    public List<ManilaPlugin> Plugins = [];

    /// <summary>
    /// Initializes the extension manager with the plugin directory.
    /// </summary>
    /// <param name="pluginDir">The directory that will be searched. Does not support recursive search of subdirectories.</param>
    public void Init(string pluginDir) {
        this.PluginDir = pluginDir;
    }
    /// <summary>
    /// Loads all plugins from the plugin directory.
    /// </summary>
    /// <exception cref="Exception">Plugin instance could not be created.</exception>
    public void LoadPlugins() {
        if (PluginDir == null) throw new Exception("Plugin directory not set");

        if (!Directory.Exists(PluginDir)) {
            Logger.Warn("Plugin directory does not exist: " + PluginDir);
            Logger.Info("Skipping plugin loading");
            return;
        }

        foreach (var file in Directory.GetFiles(PluginDir, "*.dll")) {
            var assembly = Assembly.LoadFile(Path.Join(Directory.GetCurrentDirectory(), file));
            foreach (var type in assembly.GetTypes()) {
                if (type.IsSubclassOf(typeof(ManilaPlugin))) {
                    var plugin = (ManilaPlugin?) Activator.CreateInstance(type);
                    if (plugin == null) throw new Exception("Failed to create plugin instance of type " + type + " loaded from " + file);
                    plugin.File = file;
                    Plugins.Add(plugin);

                    foreach (var dep in plugin.NugetDependencies) {
                        var match = nugetDependencyPattern.Match(dep);
                        if (!match.Success) throw new Exception("Invalid dependency: " + dep);
                        var package = match.Groups["package"].Value;
                        var version = match.Groups["version"].Value;
                        if (version == String.Empty) throw new Exception("Invalid dependency: " + dep + " (version is empty)");
                        Logger.Info("Plugin " + plugin.Name + " has dependency: " + package + (version == null ? "" : "@" + version));
                    }

                    foreach (var prop in type.GetProperties()) {
                        if (prop.GetCustomAttribute<PluginInstance>() != null)
                            prop.SetValue(null, plugin);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Initializes all loaded plugins.
    /// </summary>
    public void InitPlugins() {
        foreach (var plugin in Plugins) {

            plugin.Init();
        }
    }
    /// <summary>
    /// Releases all loaded plugins.
    /// </summary>
    public void ReleasePlugins() {
        foreach (var plugin in Plugins) {
            plugin.Release();
        }
    }

    /// <summary>
    /// Returns a plugin by its type by using a generic.
    /// </summary>
    /// <typeparam name="T">The plugin type</typeparam>
    /// <returns>The plugin instance</returns>
    /// <exception cref="Exception">Plugin has noot been found.</exception>
    public ManilaPlugin GetPlugin<T>() {
        return GetPlugin(typeof(T));
    }
    /// <summary>
    /// Returns a plugin by its type by passing the type as a parameter.
    /// </summary>
    /// <param name="type">The type</param>
    /// <returns>The plugin instance</returns>
    /// <exception cref="Exception"></exception>
    public ManilaPlugin GetPlugin(Type type) {
        foreach (var plugin in Plugins) {
            if (plugin.GetType() == type) return plugin;
        }
        throw new Exception("Plugin not found: " + type);
    }

    /// <summary>
    /// Returns a plugin by its group, name and version.
    /// </summary>
    /// <param name="group">The group</param>
    /// <param name="name">The name</param>
    /// <param name="version">The version</param>
    /// <returns>The instance of the plugin</returns>
    /// <exception cref="Exception">Plugin has not been found.</exception>
    public ManilaPlugin GetPlugin(string group, string name, string? version = null) {
        if (version == String.Empty) version = null;
        foreach (var plugin in Plugins) {
            if (plugin.Group == group && plugin.Name == name && (version == null || plugin.Version == version)) return plugin;
        }
        throw new Exception("Plugin not found: " + group + ":" + name + (version == null ? "" : "." + version));
    }
    /// <summary>
    /// Returns a plugin component by its group, name, component and version.
    /// </summary>
    /// <param name="group">The group</param>
    /// <param name="name">The name</param>
    /// <param name="component">The component name</param>
    /// <param name="version">The version</param>
    /// <returns>The instance of the plugin component</returns>
    public PluginComponent GetPluginComponent(string group, string name, string component, string? version = null) {
        return GetPlugin(group, name, version).GetComponent(component);
    }

    /// <summary>
    /// Returns a plugin by its key.
    /// </summary>
    /// <param name="key">Key compliant to the regex <see cref="pluginPattern"/></param>
    /// <returns>The instance of the plugin</returns>
    /// <exception cref="Exception">Plugin key was invalid</exception>
    public ManilaPlugin GetPlugin(string key) {
        var match = pluginPattern.Match(key);
        if (!match.Success) throw new Exception("Invalid plugin key: " + key);
        return GetPlugin(match.Groups["group"].Value, match.Groups["name"].Value, match.Groups["version"].Value);
    }
    /// <summary>
    /// Returns a plugin component by its key.
    /// </summary>
    /// <param name="key">Key compliant to the regex <see cref="componentPattern"/></param>
    /// <returns>The instance of the component</returns>
    /// <exception cref="Exception">Component key was invalid</exception>
    public PluginComponent GetPluginComponent(string key) {
        var match = componentPattern.Match(key);
        if (!match.Success) throw new Exception("Invalid component key: " + key);
        return GetPluginComponent(match.Groups["group"].Value, match.Groups["name"].Value, match.Groups["component"].Value, match.Groups["version"].Value);
    }

    /// <summary>
    /// Returns a plugin API class by its key.
    /// </summary>
    /// <param name="key">The key</param>
    /// <returns>The type</returns>
    /// <exception cref="Exception">Class was not found or regex was incorrect</exception>
    public Type GetAPIType(string key) {
        var match = apiClassPattern.Match(key);
        if (!match.Success) throw new Exception("Invalid API class key: " + key);
        return GetPlugin(match.Groups["group"].Value, match.Groups["name"].Value, match.Groups["version"].Value).GetAPIClass(match.Groups["class"].Value);
    }
}
