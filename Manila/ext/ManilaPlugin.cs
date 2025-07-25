using System.Collections.Concurrent;
using Shiron.Manila.API;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Logging;
using Shiron.Manila.Utils;

namespace Shiron.Manila.Ext;

/// <summary>
/// Represents a Manila plugin.
/// </summary>
public abstract class ManilaPlugin(string group, string name, string version, List<string> authors, List<string>? nugetDependencies) {
    public readonly string Group = group;
    public readonly string Name = name;
    public readonly string Version = version;
    public readonly List<string> Authors = authors;
    public readonly List<string> NugetDependencies = nugetDependencies ?? [];

    public readonly ConcurrentDictionary<string, PluginComponent> Components = [];
    public readonly List<Type> Enums = [];
    public readonly List<Type> Dependencies = [];
    public string? File { get; internal set; } = null;
    public readonly Dictionary<string, Type> APIClasses = [];
    public readonly Dictionary<string, ProjectTemplate> ProjectTemplates = [];

    internal ILogger? _logger { get; private set; }

    internal void SetLogger(ILogger logger) {
        _logger = logger;
    }

    /// <summary>
    /// Called upon initialization of the plugin.
    /// </summary>
    public abstract void Init();
    /// <summary>
    /// Called upon release of the plugin.
    /// </summary>
    public abstract void Release();

    /// <summary>
    /// Prints a debug severity message.
    /// </summary>
    /// <param name="args">The message</param>
    public void Debug(params object[] args) { _logger?.Log(new BasicPluginLogEntry(this, string.Join(" ", args), LogLevel.Debug)); }
    /// <summary>
    /// Prints a information severity message.
    /// </summary>
    /// <param name="args">The message</param>
    public void Info(params object[] args) { _logger?.Log(new BasicPluginLogEntry(this, string.Join(" ", args), LogLevel.Info)); }
    /// <summary>
    /// Prints a warning severity message.
    /// </summary>
    /// <param name="args">The message</param>
    public void Warn(params object[] args) { _logger?.Log(new BasicPluginLogEntry(this, string.Join(" ", args), LogLevel.Warning)); }
    /// <summary>
    /// Prints a error severity message.
    /// </summary>
    /// <param name="args">The message</param>
    public void Error(params object[] args) { _logger?.Log(new BasicPluginLogEntry(this, string.Join(" ", args), LogLevel.Error)); }
    /// <summary>
    /// Prints a critical severity message.
    /// </summary>
    /// <param name="args">The message</param>
    public void Critical(params object[] args) { _logger?.Log(new BasicPluginLogEntry(this, string.Join(" ", args), LogLevel.Critical)); }

    /// <summary>
    /// Registers a component to the plugin.
    /// </summary>
    /// <param name="component">The instance the component</param>
    /// <exception cref="Exception">Component already registered to this plugin</exception>
    public void RegisterComponent(PluginComponent component) {
        if (Components.ContainsKey(component.Name)) throw new ManilaException("Component already registered to this plugin.");
        Components[component.Name] = component;
        component._plugin = this;
    }
    /// <summary>
    /// Registers an enum to the plugin. The class requires the <see cref="ScriptEnum"/> attribute.
    /// </summary>
    /// <typeparam name="T">The class type</typeparam>
    public void RegisterEnum<T>() {
        Enums.Add(typeof(T));
    }
    public void RegisterDependency<T>() {
        Dependencies.Add(typeof(T));
    }
    public void RegisterAPIType<T>(string name) {
        APIClasses.Add(name, typeof(T));
    }
    public void RegisterProjectTemplate(ProjectTemplate template) {
        var name = template.Name;
        if (ProjectTemplates.ContainsKey(name)) {
            throw new ManilaException($"Project template with name '{name}' already registered in plugin '{Group}.{Name}'.");
        }
        ProjectTemplates[name] = template;
    }

    /// <summary>
    /// Returns a string representation of the plugin.
    /// </summary>
    /// <returns>Format: ManilaPlugin(Group:Name@Version)</returns>
    public override string ToString() {
        return $"ManilaPlugin({new RegexUtils.PluginMatch(Group, Name, Version).Format()})";
    }

    /// <summary>
    /// Returns the plugin directory path.
    /// </summary>
    /// <returns>The directory the plugin is allowed to write/read to</returns>
    public string GetDataDir() {
        return Path.Join(".manila", "plugins", $"{Group}.{Name}");
    }
}
