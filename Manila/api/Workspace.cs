namespace Shiron.Manila.API;

using Shiron.Manila.Attributes;

public class Workspace {
	public Dir _path { get; private set; }

	public Dictionary<string, Project> projects { get; } = new();

	public Workspace(string location) {
		_path = new Dir(location);
	}

	public Dir path() {
		return _path;
	}
}
