
using Newtonsoft.Json;
using Shiron.Manila.API.Ext;
using Shiron.Manila.API.Interfaces;

namespace Shiron.Manila.API.Logging;

/// <summary>
/// Represents a snapshot of a plugin's information for logging.
/// </summary>
public sealed class PluginInfo {
    public string Name { get; init; }
    public string Group { get; init; }
    public string Version { get; init; }
    public string[] Authors { get; init; }
    public string Entry { get; init; }
    public string[] NuGetDependencies { get; init; }
    public string File { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginInfo"/> class from a plugin instance.
    /// </summary>
    public PluginInfo(ManilaPlugin plugin) {
        Name = plugin.Name;
        Group = plugin.Group;
        Version = plugin.Version;
        Authors = plugin.Authors.ToArray();
        Entry = plugin.GetType().FullName!;
        NuGetDependencies = plugin.NugetDependencies.ToArray();
        File = plugin.File!;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginInfo"/> class with specified properties.
    /// </summary>
    [JsonConstructor]
    public PluginInfo(string name, string group, string version, string[] authors, string entry, string[] nuGetDependencies, string file) {
        Name = name;
        Group = group;
        Version = version;
        Authors = authors;
        Entry = entry;
        NuGetDependencies = nuGetDependencies;
        File = file;
    }
}

/// <summary>
/// Represents a snapshot of a job's information for logging.
/// </summary>
public sealed class JobInfo {
    public string Name { get; init; }
    public string ID { get; init; }
    public string ScriptPath { get; init; }
    public string Description { get; init; }
    public ComponentInfo Component { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="JobInfo"/> class from a job instance.
    /// </summary>
    public JobInfo(Job job) {
        Name = job.Name;
        ID = job.GetIdentifier();
        ScriptPath = job.Context.ScriptPath;
        Description = job.Description;
        Component = new ComponentInfo(job.Component);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JobInfo"/> class with specified properties.
    /// </summary>
    [JsonConstructor]
    public JobInfo(string name, string id, string scriptPath, string description, ComponentInfo component) {
        Name = name;
        ID = id;
        ScriptPath = scriptPath;
        Description = description;
        Component = component;
    }
}

/// <summary>
/// Represents a snapshot of a component's information for logging.
/// </summary>
public sealed class ComponentInfo {
    public bool IsProject { get; init; }
    public bool IsWorkspace { get; init; }
    public string Root { get; init; }
    public string ID { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ComponentInfo"/> class from a component instance.
    /// </summary>
    public ComponentInfo(Component component) {
        IsProject = component is Project;
        IsWorkspace = component is Workspace;
        Root = component.Path.Get();
        ID = component.GetIdentifier();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ComponentInfo"/> class with specified properties.
    /// </summary>
    [JsonConstructor]
    public ComponentInfo(bool isProject, bool isWorkspace, string root, string id) {
        IsProject = isProject;
        IsWorkspace = isWorkspace;
        Root = root;
        ID = id;
    }
}

/// <summary>
/// Represents a snapshot of a project's information for logging.
/// </summary>
public sealed class ProjectInfo {
    public string Name { get; init; }
    public string Identifier { get; init; }
    public string? Version { get; init; }
    public string? Group { get; init; }
    public string? Description { get; init; }
    public string Root { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectInfo"/> class from a project instance.
    /// </summary>
    public ProjectInfo(Project project) {
        Name = project.Name;
        Identifier = project.GetIdentifier();
        Version = project.Version;
        Group = project.Group;
        Description = project.Description;
        Root = project.Path.Get();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectInfo"/> class with specified properties.
    /// </summary>
    [JsonConstructor]
    public ProjectInfo(string name, string identifier, string? version, string? group, string? description, string root) {
        Name = name;
        Identifier = identifier;
        Version = version;
        Group = group;
        Description = description;
        Root = root;
    }
}

/// <summary>
/// Represents a snapshot of an executable object's information for logging.
/// </summary>
public sealed class ExecutableObjectInfo {
    public string ID { get; init; }
    public string Type { get; init; }
    public bool Blocking { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExecutableObjectInfo"/> class from an executable object.
    /// </summary>
    public ExecutableObjectInfo(ExecutableObject obj) {
        ID = obj.GetID();
        Type = obj.GetType().FullName ?? "Unknown";
        Blocking = obj.IsBlocking();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExecutableObjectInfo"/> class with specified properties.
    /// </summary>
    [JsonConstructor]
    public ExecutableObjectInfo(string id, string type, bool blocking) {
        ID = id;
        Type = type;
        Blocking = blocking;
    }
}
