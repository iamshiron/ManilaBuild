using Shiron.Manila.Utils;

namespace Shiron.Manila.API;

public class Task {
	public readonly string name;
	public readonly List<string> dependencies = new();
	private Action? action;
	private readonly ScriptContext context;
	private readonly Project project;

	public Task(string name, Project project, ScriptContext context) {
		this.name = name;
		this.project = project;
		this.context = context;
	}

	public Task after(string task) {
		dependencies.Add(task);
		return this;
	}
	public Task execute(dynamic action) {
		this.action = () => {
			try {
				action();
			} catch (Exception e) {
				Logger.error("Task failed: " + name);
				Logger.error(e.GetType().Name + ": " + e.Message);
				throw;
			}
		};
		return this;
	}
}
