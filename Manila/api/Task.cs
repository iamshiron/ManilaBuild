using Shiron.Manila.Utils;

namespace Shiron.Manila.API;

public class Task {
	public readonly string name;
	public readonly List<string> dependencies = new();
	public Action? action { get; private set; }
	private readonly ScriptContext context;
	private readonly Component component;

	public string getIdentifier() {
		return $"{component.getIdentifier()}:{name}";
	}

	public Task(string name, Component component, ScriptContext context) {
		this.name = name;
		this.component = component;
		this.context = context;
		this.component.tasks.Add(this);
	}

	public Task after(string task) {
		if (task.StartsWith(":")) {
			dependencies.Add(task[1..]);
		} else {
			dependencies.Add($"{component.getIdentifier()}:{task}");
		}

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

	public List<string> getExecutionOrder() {
		List<string> result = new();
		foreach (string dependency in dependencies) {
			Task? dependentTask = ManilaEngine.getInstance().workspace.getTask(dependency);
			if (dependentTask == null) { Logger.warn("Task not found: " + dependency); continue; }
			List<string> dependencyOrder = dependentTask.getExecutionOrder();
			foreach (string depTask in dependencyOrder) {
				if (!result.Contains(depTask)) {
					result.Add(depTask);
				}
			}

		}
		if (!result.Contains(getIdentifier())) {
			result.Add(getIdentifier());
		}

		return result;
	}
}
