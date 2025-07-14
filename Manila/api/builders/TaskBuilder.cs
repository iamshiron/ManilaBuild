
using Shiron.Manila.Exceptions;
using Shiron.Manila.Logging;
using Shiron.Manila.Utils;

namespace Shiron.Manila.API.Builders;

public sealed class TaskBuilder(string name, ScriptContext context, Component component, ArtifactBuilder? artifactBuilder) : IBuildable<Task> {
    public readonly string Name = name;
    public readonly ArtifactBuilder? ArtifactBuilder = artifactBuilder;
    public string Description { get; private set; } = "A generic task";
    public bool Blocking { get; private set; } = true;

    public readonly List<string> Dependencies = [];
    public ITaskAction[] Actions { get; private set; } = [];
    public readonly ScriptContext ScriptContext = context;
    public readonly Component Component = component;

    /// <summary>
    /// Add a dependency to the task.
    /// </summary>
    /// <param name="task">The dependents task ID</param>
    /// <returns>Task instance for chaining calls</returns>
    public TaskBuilder after(string task) {
        if (task.Contains(":") || task.Contains("/")) {
            if (!RegexUtils.IsValidTaskRegex(task)) throw new ManilaException($"Invalid task regex {task}!");
            Dependencies.Add(task);
            return this;
        }

        var match = new RegexUtils.TaskMatch(Component is Workspace ? null : Component.GetIdentifier(), ArtifactBuilder == null ? null : ArtifactBuilder.Name, task);
        Dependencies.Add(RegexUtils.FromTaskMatch(match));

        return this;
    }
    /// <summary>
    /// Add a dependency to the task.
    /// </summary>
    /// <param name="task">The dependents task ID</param>
    /// <returns>Task instance for chaining calls</returns>
    public TaskBuilder after(string[] task) {
        foreach (var t in task) after(t);
        return this;
    }
    /// <summary>
    /// The action to be executed by the task.
    /// </summary>
    /// <param name="action">The action</param>
    /// <returns>Task instance for chaining calls</returns>
    public TaskBuilder execute(object o) {
        if (o is ITaskAction action) {
            Logger.Debug($"Found task action of type {action.GetType().FullName}");
            Actions = [action];
        } else
        if (o is IList<object> list) {
            Logger.Debug($"Found {list.Count} chained actions!");
            Actions = list.Cast<ITaskAction>().ToArray();
        } else {
            Actions = [new TaskScriptAction((dynamic) o)];
        }

        return this;
    }
    /// <summary>
    /// Set the description of the task.
    /// </summary>
    /// <param name="description">The description</param>
    /// <returns>Task instance for chaining calls</returns>
    public TaskBuilder description(string description) {
        this.Description = description;
        return this;
    }
    /// <summary>
    /// Sets a task's blocking mode, meaning if it will block the execution flow or is running in the background
    /// </summary>
    /// <param name="background">True: Non Blocking, False: Blocking</param>
    /// <returns></returns>
    public TaskBuilder background(bool background = true) {
        this.Blocking = !background;
        return this;
    }

    public Task Build() {
        return new(this);
    }
}
