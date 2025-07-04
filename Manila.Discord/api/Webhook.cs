using System.Text;
using System.Text.Json;
using Discord.Net;
using Discord.Webhook;

namespace Shiron.Manila.Discord.API;

public class Webhook {
    public class Impl(string url) {
        /*public string Url { get; init; } = url;

        public async Task send(string message) {
            // Create the payload
            var payload = new {
                content = message
            };

            // Serialize the payload to JSON
            string jsonPayload = JsonSerializer.Serialize(payload);

            // Create HTTP client
            using HttpClient client = new HttpClient();
            // Create the content with proper content type
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            // Send the POST request
            HttpResponseMessage response = await client.PostAsync(Url, content);

            // Check if the request was successful
            if (!response.IsSuccessStatusCode) {
                string errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"Failed to send message to Discord webhook. Status: {response.StatusCode}, Response: {errorContent}");
            }
        }
        public void sendSync(string content) {
            Task.Run(() => send(content)).Wait();
        }*/

        public DiscordWebhookClient Webhook = new(url);

        public async Task send(string message) {
            await Webhook.SendMessageAsync(message);
        }
        public void sendSync(string message) {
            Webhook.SendMessageAsync(message).GetAwaiter().GetResult();
        }
    }

    public Impl create(string url) {
        ManilaDiscord.Instance.Info($"Creating webhook with URL: {url}");
        return new Impl(url);
    }
}
