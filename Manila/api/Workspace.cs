using Shiron.Manila.Exceptions;
using Shiron.Manila.Logging;
using Shiron.Manila.Utils;

namespace Shiron.Manila.API;

/// <summary>
/// Represents a workspace containing projects.
/// </summary>
public class Workspace(ILogger logger, string location) : Component(logger, location, location) { // The workspace always lies within the root directory
    public Dictionary<string, Project> Projects { get; } = new();
    public List<Tuple<ProjectFilter, Action<Project>>> ProjectFilters { get; } = new();

    public override string GetIdentifier() {
        return "";
    }

    public override string ToString() {
        return $"Workspace()";
    }
}
