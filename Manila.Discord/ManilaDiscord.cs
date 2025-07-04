namespace Shiron.Manila.Discord;

using Shiron.Manila.API;
using Shiron.Manila.Ext;
using Shiron.Manila.Attributes;

public class ManilaDiscord : ManilaPlugin {
    public ManilaDiscord() : base("shiron.manila", "discord", "1.0.0", ["Shiron"], ["Discord.Net@3.17.4"]) {
    }

    [PluginInstance]
    public static ManilaDiscord Instance { get; set; }

    public override void Init() {
        RegisterAPIType<API.Webhook>("webhook");
    }

    public override void Release() {
    }
}
