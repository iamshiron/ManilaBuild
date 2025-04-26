namespace Shiron.Manila.Discord;

using Shiron.Manila.API;
using Shiron.Manila.Ext;
using Shiron.Manila.Attributes;

public class ManilaDiscord : ManilaPlugin {
    public ManilaDiscord() : base("shiron.manila", "discord", "1.0.0", "Shiron") {
    }

    [PluginInstance]
    public static ManilaDiscord Instance { get; set; }

    public override void Init() {
    }

    public override void Release() {
    }
}
