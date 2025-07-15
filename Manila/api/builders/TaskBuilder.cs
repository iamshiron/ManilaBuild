using System.Diagnostics.CodeAnalysis;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Logging;
using Shiron.Manila.Utils;

namespace Shiron.Manila.API.Builders;

/// <summary>
/// Builder for creating tasks within a Manila build configuration.
/// </summary>
public sealed class TaskBuilder(string name, ScriptContext context, Component component, ArtifactBuilder? artifactBuilder) : IBuildable<Task> {
    /// <summary>
    /// The name of the task.
    /// </summary>
    public readonly string Name = name;

    /// <summary>
    /// The artifact builder this task belongs to, if any.
    /// </summary>
    public readonly ArtifactBuilder? ArtifactBuilder = artifactBuilder;

    /// <summary>
    /// Description of what the task does.
    /// </summary>
    public string Description { get; private set; } = "A generic task";

    /// <summary>
    /// Whether the task blocks execution flow until completion.
    /// </summary>
    public bool Blocking { get; private set; } = true;

    /// <summary>
    /// List of task dependencies that must execute before this task.
    /// </summary>
    public readonly List<string> Dependencies = [];

    /// <summary>
    /// Array of actions to be executed by this task.
    /// </summary>
    public ITaskAction[] Actions { get; private set; } = [];

    /// <summary>
    /// The script context for this task.
    /// </summary>
    public readonly ScriptContext ScriptContext = context;

    /// <summary>
    /// The component this task belongs to.
    /// </summary>
    public readonly Component Component = component;

    /// <summary>
    /// Add a dependency to the task.
    /// </summary>
    /// <param name="task">The dependents task ID</param>
    /// <returns>Task instance for chaining calls</returns>
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Exposed to JavaScript context")]
    public TaskBuilder after(string task) {
        if (task.Contains(":") || task.Contains("/")) {
            if (!RegexUtils.IsValidTask(task)) throw new ManilaException($"Invalid task regex {task}!");
            Dependencies.Add(task);
            return this;
        }

        var match = new RegexUtils.TaskMatch(Component is Workspace ? null : Component.GetIdentifier(), ArtifactBuilder?.Name, task);
        Dependencies.Add(match.Format());

        return this;
    }
    /// <summary>
    /// Add a dependency to the task.
    /// </summary>
    /// <param name="task">The dependents task ID</param>
    /// <returns>Task instance for chaining calls</returns>
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Exposed to JavaScript context")]
    public TaskBuilder after(string[] task) {
        foreach (var t in task) after(t);
        return this;
    }
    /// <summary>
    /// The action to be executed by the task.
    /// </summary>
    /// <param name="action">The action</param>
    /// <returns>Task instance for chaining calls</returns>
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Exposed to JavaScript context")]
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
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Exposed to JavaScript context")]
    public TaskBuilder description(string description) {
        this.Description = description;
        return this;
    }
    /// <summary>
    /// Sets a task's blocking mode, meaning if it will block the execution flow or is running in the background
    /// </summary>
    /// <param name="background">True: Non Blocking, False: Blocking</param>
    /// <returns></returns>
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Exposed to JavaScript context")]
    public TaskBuilder background(bool background = true) {
        this.Blocking = !background;
        return this;
    }

    /// <summary>
    /// Builds the task using the configured properties and actions.
    /// </summary>
    /// <returns>The built task instance.</returns>
    public Task Build() {
        return new(this);
    }
}
