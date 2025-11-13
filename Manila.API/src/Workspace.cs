using Shiron.Manila.Exceptions;
using Shiron.Manila.Logging;
using Shiron.Manila.Utils;

namespace Shiron.Manila.API;

public record ProjectFilterHook(ProjectFilter Filter, Action<Project> Action);

/// <summary>Root script scope; owns all projects.</summary>
/// <param name="logger">Logger instance.</param>
/// <param name="location">Root workspace directory.</param>
public class Workspace(ILogger logger, string location) : Component(logger, location, location) { // The workspace always lies within the root directory
    /// <summary>Projects keyed by name.</summary>
    public Dictionary<string, Project> Projects { get; } = [];

    /// <summary>Registered project filter hooks.</summary>
    public List<ProjectFilterHook> ProjectFilters { get; } = [];

    /// <summary>Workspace identifier (empty; workspace is root).</summary>
    /// <returns>Empty string.</returns>
    public override string GetIdentifier() {
        return "";
    }

    /// <summary>Debug string.</summary>
    /// <returns>Formatted workspace string.</returns>
    public override string ToString() {
        return $"Workspace()";
    }
}
