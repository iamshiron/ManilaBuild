using System.Reflection;
using System.Text;
using System.Text.Json;
using Discord.Net;
using Discord.Webhook;
using Shiron.Manila.Profiling;

namespace Shiron.Manila.Discord.API;

public class Webhook {
    public class Impl(string url) {
        public readonly string _url = url;
        private static readonly HttpClient _httpClient = new();

        public async Task send(string message) {
            var payload = new { content = message };
            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            try {
                var response = await _httpClient.PostAsync(_url, content);
                response.EnsureSuccessStatusCode();
            } catch (HttpRequestException e) {
                ManilaDiscord.Instance.Error($"Error sending webhook: {e.Message}");
                throw;
            }
        }

        public void sendSync(string message) {
            send(message).GetAwaiter().GetResult();
        }
    }

    public Impl create(string url) {
        return new Impl(url);
    }
}
