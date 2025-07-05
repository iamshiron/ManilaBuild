using System.Reflection;
using System.Text;
using System.Text.Json;
using Discord.Net;
using Discord.Webhook;
using Shiron.Manila.Profiling;

namespace Shiron.Manila.Discord.API;

public class Webhook {
    public class Impl(string url) {
        public DiscordWebhookClient Webhook = new(url);

        public async Task send(string message) {
            await Webhook.SendMessageAsync(message);
        }
        public void sendSync(string message) {
            Webhook.SendMessageAsync(message).GetAwaiter().GetResult();
        }
    }

    public Impl create(string url) {
        return new Impl(url);
    }
}
