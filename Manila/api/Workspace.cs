namespace Shiron.Manila.API;

using Shiron.Manila.Attributes;

public class Workspace : Component {
	public Dictionary<string, Project> projects { get; } = new();
	public List<Tuple<ProjectFilter, Action<Project>>> projectFilters { get; } = new();

	public Workspace(string location) : base(location) {
	}

	public Task getTask(string identifier) {
		if (!identifier.Contains(":")) return getTask(identifier, null);
		return getTask(
			identifier[(identifier.LastIndexOf(":") + 1)..],
			identifier[..identifier.LastIndexOf(":")]
		);
	}
	public Task getTask(string task, string? project = null) {
		if (project == string.Empty) project = null;
		if (project == null) return tasks.First(t => t.name == task);
		if (project.StartsWith(":")) project = project[1..];
		return projects[project].tasks.First(t => t.name == task);
	}

	public override string getIdentifier() {
		return "";
	}
}
