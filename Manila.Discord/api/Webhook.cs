using System.Text;
using System.Text.Json;

namespace Shiron.Manila.Discord.API;

public class Webhook {
    public class Impl(string url) {
        private readonly string _url = url;
        private static readonly HttpClient _httpClient = new();

        public async Task Send(string message) {
            var payload = new { content = message };
            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            try {
                var response = await _httpClient.PostAsync(_url, content);
                response.EnsureSuccessStatusCode();
            } catch (HttpRequestException e) {
                ManilaDiscord.Instance?.Error($"Error sending webhook: {e.Message}");
                throw;
            }
        }
    }

    public static Impl Create(string url) {
        return new Impl(url);
    }
}
