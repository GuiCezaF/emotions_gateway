using emotions_gateway.DTOs;
using emotions_gateway.Utils;
using StackExchange.Redis;
using System.Text;
using System.Text.Json;

namespace emotions_gateway.Services;

public class ChatWebSocketService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly HttpClient _httpClient;

    public ChatWebSocketService(IConnectionMultiplexer redis)
    {
        _redis = redis;
        _httpClient = new HttpClient();
    }

    public async Task<(string responseJson, bool isError)> ProcessMessageAsync(string receivedJson, CancellationToken cancellationToken)
    {
        try
        {
            using var doc = JsonDocument.Parse(receivedJson);
            var text = doc.RootElement.GetProperty("text").GetString() ?? string.Empty;

            var db = _redis.GetDatabase();
            var lastEmotionJson = await db.StringGetAsync("last_emotion");

            string emotion = "unknown";
            string? emotionUserId = null;

            if (!lastEmotionJson.IsNullOrEmpty)
            {
                using var emotionDoc = JsonDocument.Parse((string)lastEmotionJson!);
                var root = emotionDoc.RootElement;

                if (root.TryGetProperty("emotion", out var e))
                    emotion = e.GetString() ?? "unknown";

                if (root.TryGetProperty("user_id", out var u))
                    emotionUserId = u.GetString();
            }

            var externalResponse = await SafeSendAsync(new
            {
                user_id = emotionUserId,
                message = text,
                emotion
            });

            var responseObj = new
            {
                type = "chat_response",
                message = externalResponse?.reply ?? "No message",
                emotion_used = externalResponse?.emotion_used ?? emotion,
                user_id = emotionUserId,
                timestamp = DateTime.UtcNow
            };

            return (JsonSerializer.Serialize(responseObj), false);
        }
        catch (JsonException)
        {
            var errorObj = new
            {
                type = "error",
                message = "Invalid JSON message. Expected format: { \"text\": \"your message\" }",
                timestamp = DateTime.UtcNow
            };

            return (JsonSerializer.Serialize(errorObj), true);
        }
    }

    private async Task<ExternalApiResponse?> SafeSendAsync(object data)
    {
        var jsonPayload = JsonSerializer.Serialize(data);
        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync("http://chatbot-service:8001/chat", content);
            response.EnsureSuccessStatusCode();

            var responseString = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ExternalApiResponse>(responseString);

            return result;
        }
        catch (Exception ex)
        {
            LogError.Log("[ErrorEventArgs] Erro ao enviar dados para a API externa", ex);
            return null;
        }
    }
}
