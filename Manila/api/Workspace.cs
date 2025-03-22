namespace Shiron.Manila.API;

using Shiron.Manila.Attributes;

public class Workspace : Component {
	public Dictionary<string, Project> projects { get; } = new();
	public List<Tuple<ProjectFilter, Action<Project>>> projectFilters { get; } = new();

	public Workspace(string location) : base(location) {
	}
}
