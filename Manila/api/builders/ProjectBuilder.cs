
using Shiron.Manila.API.Containers;
using Shiron.Manila.Utils;

namespace Shiron.Manila.API.Builders;

public class ProjectBuilder(string path, string name) : ComponentBuilder(path), IBuildable<ProjectContainer> {
    public readonly string Name = name;

    public new ProjectContainer Build() {
        return new ProjectContainer(
            base.Build(),
            Name
        );
    }
}
