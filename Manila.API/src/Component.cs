using System.Dynamic;
using System.Reflection;
using Newtonsoft.Json;
using Shiron.Manila.Exceptions;
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
    public DirHandle Path { get; private set; } = new DirHandle(path);

    /// <summary>
    /// Collection of jobs belonging to this component.
    /// </summary>
    public List<Job> Jobs { get; } = [];

    /// <summary>
    /// Returns a unique identifier for this component.
    /// </summary>
    /// <returns>The component identifier.</returns>
    public virtual string GetIdentifier() {
        string relativeDir = System.IO.Path.GetRelativePath(RootDir, Path.Handle);
        return relativeDir.Replace(System.IO.Path.DirectorySeparatorChar, ':').ToLower();
    }

    /// <summary>
    /// Finalizes the component by building all jobs.
    /// </summary>
    /// <param name="manilaAPI">The Manila API instance.</param>
    public virtual void Finalize(Manila manilaAPI) {
        Jobs.AddRange(manilaAPI.JobBuilders.Select(b => b.Build()));
    }
}
