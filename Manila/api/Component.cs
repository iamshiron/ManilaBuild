using System.Dynamic;
using System.Reflection;
using Microsoft.ClearScript;
using Newtonsoft.Json;
using Shiron.Manila.Attributes;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Ext;
using Shiron.Manila.Logging;
using Shiron.Manila.Utils;

namespace Shiron.Manila.API;

/// <summary>
/// Represents a component in the build script that groups jobs and plugins.
/// </summary>
public class Component(ILogger logger, string rootDir, string path) {
    public readonly string RootDir = rootDir;
    private readonly ILogger _logger = logger;

    /// <summary>
    /// The directory path of this component.
    /// </summary>
    [ScriptProperty(true)]
    public DirHandle Path { get; private set; } = new DirHandle(path);

    /// <summary>
    /// Collection of plugin components applied to this component.
    /// </summary>
    public Dictionary<Type, PluginComponent> PluginComponents { get; } = [];

    /// <summary>
    /// List of plugin types applied to this component.
    /// </summary>
    public List<Type> Plugins { get; } = [];

    /// <summary>
    /// Collection of jobs belonging to this component.
    /// </summary>
    public List<Job> Jobs { get; } = [];

    /// <summary>
    /// Types of dependencies used by this component.
    /// </summary>
    public List<Type> DependencyTypes { get; } = [];

    /// <summary>
    /// Returns a unique identifier for this component.
    /// </summary>
    /// <returns>The component identifier.</returns>
    public virtual string GetIdentifier() {
        string relativeDir = System.IO.Path.GetRelativePath(RootDir, Path.Handle);
        return relativeDir.Replace(System.IO.Path.DirectorySeparatorChar, ':').ToLower();
    }

    /// <summary>
    /// Checks if a component type is applied to this component.
    /// </summary>
    /// <typeparam name="T">The component type to check.</typeparam>
    /// <returns>True if the component is applied.</returns>
    public bool HasComponent<T>() where T : PluginComponent {
        return PluginComponents.ContainsKey(typeof(T));
    }
    /// <summary>
    /// Gets a component instance of the specified type.
    /// </summary>
    /// <typeparam name="T">The component type to retrieve.</typeparam>
    /// <returns>The component instance.</returns>
    /// <exception cref="Exception">Thrown when the component is not found.</exception>
    public T GetComponent<T>() where T : PluginComponent {
        if (PluginComponents.TryGetValue(typeof(T), out var component))
            return (T) component;

        foreach (var p in PluginComponents) {
            if (typeof(T).IsAssignableFrom(p.Key))
                return (T) p.Value;
        }

        throw new Exception($"Component of type {typeof(T).Name} not found in this context.");
    }

    /// <summary>
    /// Gets the language component applied to this component.
    /// </summary>
    /// <returns>The language component instance.</returns>
    /// <exception cref="Exception">Thrown when no language component is found.</exception>
    public LanguageComponent GetLanguageComponent() {
        foreach (var component in PluginComponents.Values) {
            if (component is LanguageComponent languageComponent) return languageComponent;
        }
        throw new Exception("No language component found.");
    }

    /// <summary>
    /// Finalizes the component by building all jobs.
    /// </summary>
    /// <param name="manilaAPI">The Manila API instance.</param>
    public virtual void Finalize(Manila manilaAPI) {
        Jobs.AddRange(manilaAPI.JobBuilders.Select(b => b.Build()));
    }
}
