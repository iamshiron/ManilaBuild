namespace Shiron.Manila.API;

public class Workspace {
	public Dir location { get; private set; }

	public Dictionary<string, Project> projects { get; } = new();

	public Workspace(string location) {
		this.location = new Dir(location);
	}
}
