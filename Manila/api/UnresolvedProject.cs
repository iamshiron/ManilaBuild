namespace Shiron.Manila.API;

public class UnresolvedProject {
	public readonly string identifier;

	public UnresolvedProject(string identifier) { this.identifier = identifier; }

	public Project resolve() {
		foreach (var pair in ManilaEngine.getInstance().workspace.projects) {
			if (pair.Value.getIdentifier() == identifier) {
				return pair.Value;
			}
		}
		throw new Exception("Project not found: " + identifier);
	}

	public static implicit operator Project(UnresolvedProject unresolved) {
		return unresolved.resolve();
	}
}
