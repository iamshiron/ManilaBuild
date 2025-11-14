using System.Collections.Concurrent;
using Shiron.Manila.API;
using Shiron.Manila.API.Interfaces.Artifacts;
using Shiron.Manila.API.Logging;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Logging;
using Shiron.Manila.Utils;

namespace Shiron.Manila.API.Ext;

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
    public string? File { get; set; } = null;
    public readonly Dictionary<string, Type> APIClasses = [];
    public readonly Dictionary<string, ProjectTemplate> ProjectTemplates = [];
    public readonly List<Tuple<string, Type>> ArtifactBuilderTypes = [];

    internal ILogger? _logger { get; private set; }

    public void SetLogger(ILogger logger) {
        if (_logger != null) throw new ManilaException("Logger already set for this plugin.");
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
    /// Execute a shell command, logging output to the plugin logger.
    /// </summary>
    /// <param name="command">The command</param>
    /// <param name="args">The arguments</param>
    /// <param name="workingDir">The working directory</param>
    /// <returns>Exit code of the command</returns>
    public int RunCommand(string command, string[]? args = null, string? workingDir = null) {
        return ShellUtils.Run(command, args, workingDir, (msg) => Info(msg), (msg) => Error(msg));
    }

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
    /// Registers a dependency type to the plugin.
    /// </summary>
    /// <typeparam name="T">The dependency type</typeparam>
    public void RegisterDependency<T>() => Dependencies.Add(typeof(T));
    /// <summary>
    /// Registers an API type to the plugin.
    /// </summary>
    /// <typeparam name="T">The API type</typeparam>
    /// <param name="name">The name of the API type</param>
    public void RegisterAPIType<T>(string name) => APIClasses.Add(name, typeof(T));
    /// <summary>
    /// Registers an artifact builder type to the plugin.
    /// </summary>
    /// <param name="name">The name of the artifact builder</param>
    /// <param name="builder">The artifact builder type</param>
    public void RegisterArtifact(string name, Type builder) => ArtifactBuilderTypes.Add(Tuple.Create(name, builder));

    /// <summary>
    /// Registers a project template to the plugin.
    /// </summary>
    /// <param name="template">The project template</param>
    /// <exception cref="ManilaException">Project template with the same name already registered</exception>
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
