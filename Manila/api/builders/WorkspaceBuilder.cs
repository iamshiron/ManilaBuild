
using Shiron.Manila.API.Containers;
using Shiron.Manila.Utils;

namespace Shiron.Manila.API.Builders;

public class WorkspaceBuilder(string path) : ComponentBuilder(path), IBuildable<WorkspaceContainer> {
    public readonly List<ProjectBuilder> Projects = new();

    public WorkspaceBuilder AddProject(ProjectBuilder project) {
        Projects.Add(project);
        return this;
    }

    public new WorkspaceContainer Build() {
        return new WorkspaceContainer(
            base.Build(),
            Projects.Select(project => project.Build()).ToArray()
        );
    }
}
