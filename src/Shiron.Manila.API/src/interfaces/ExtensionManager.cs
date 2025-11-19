
using System.Reflection;
using Shiron.Manila.API.Ext;
using Shiron.Manila.API.Interfaces.Artifacts;
using Shiron.Utils;

namespace Shiron.Manila.API.Interfaces;

public interface IExtensionManager {
    Task LoadPluginsAsync();
    void InitPlugins();
    void ReleasePlugins();
    T GetPlugin<T>() where T : ManilaPlugin;

    ManilaPlugin GetPlugin(Type type);
    ManilaPlugin GetPlugin(string uri);
    ManilaPlugin GetPlugin(RegexUtils.PluginMatch match);

    PluginComponent GetPluginComponent(string uri);
    PluginComponent GetPluginComponent(RegexUtils.PluginComponentMatch match);

    IArtifactBlueprint GetArtifact(string uri);
    IArtifactBlueprint GetArtifact(RegexUtils.PluginComponentMatch match);

    Type GetAPIType(string uri);
    Type GetAPIType(RegexUtils.PluginApiClassMatch match);

    public List<ManilaPlugin> Plugins { get; }
    public List<Assembly> Assemblies { get; }
    public List<Type> ExposedTypes { get; }
}
