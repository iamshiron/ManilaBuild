using System.Dynamic;
using System.Reflection;
using Newtonsoft.Json;
using Shiron.Logging;
using Shiron.Manila.Exceptions;
using Shiron.Utils;

namespace Shiron.Manila.API;

/// <summary>Base container for jobs (workspace/project).</summary>
/// <param name="logger">Logger instance.</param>
/// <param name="rootDir">Root directory.</param>
/// <param name="path">Component path.</param>
public class Component(ILogger logger, string rootDir, string path) {
    /// <summary>Workspace root directory.</summary>
    public readonly string RootDir = rootDir;
    private readonly ILogger _logger = logger;

    /// <summary>Component directory handle.</summary>
    public DirHandle Path { get; private set; } = new DirHandle(path);

    /// <summary>Jobs defined on this component.</summary>
    public List<Job> Jobs { get; } = [];

    /// <summary>Component identifier (path -> colon format).</summary>
    /// <returns>Identifier string.</returns>
    public virtual string GetIdentifier() {
        string relativeDir = System.IO.Path.GetRelativePath(RootDir, Path.Handle);
        return relativeDir.Replace(System.IO.Path.DirectorySeparatorChar, ':').ToLower();
    }

    /// <summary>Materialize job builders.</summary>
    /// <param name="manilaAPI">API context.</param>
    public virtual void Finalize(Manila manilaAPI) {
        Jobs.AddRange(manilaAPI.JobBuilders.Select(b => b.Build()));
    }
}
