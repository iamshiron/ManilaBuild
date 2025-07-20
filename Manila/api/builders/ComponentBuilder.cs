
using Shiron.Manila.API.Containers;
using Shiron.Manila.Utils;

namespace Shiron.Manila.API.Builders;

public class ComponentBuilder(string path) : IBuildable<ComponentContainer> {
    public string Path { get; set; } = path;

    public ComponentContainer Build() {
        return new ComponentContainer(
            Path: this.Path
        );
    }
}
