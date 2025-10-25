using Shiron.Manila.API.Exceptions;
using Shiron.Manila.Logging;
using Shiron.Manila.Utils;

namespace Shiron.Manila.API;

public record ProjectFilterHook(ProjectFilter Filter, Action<Project> Action);

/// <summary>
/// Represents a workspace containing projects.
/// </summary>
public class Workspace(ILogger logger, string location) : Component(logger, location, location) { // The workspace always lies within the root directory
    public Dictionary<string, Project> Projects { get; } = [];
    public List<ProjectFilterHook> ProjectFilters { get; } = [];

    public override string GetIdentifier() {
        return "";
    }

    public override string ToString() {
        return $"Workspace()";
    }
}
