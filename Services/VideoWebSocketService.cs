using emotions_gateway.Utils;
using StackExchange.Redis;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace emotions_gateway.Services;

public class VideoWebSocketService
{
    private readonly IConnectionMultiplexer _redis;
    private const int BufferSize = 256 * 1024; // 256 KB
    private const string RedisChannel = "emotion_results";
    private const string RedisInputList = "emotion_frames";
    private const string RedisOutputList = "emotion_results";

    public VideoWebSocketService(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task HandleConnectionAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        var db = _redis.GetDatabase();
        var sub = _redis.GetSubscriber();
        var buffer = new byte[BufferSize];

        // 🔹 Escuta resultados e envia ao cliente
        await sub.SubscribeAsync(RedisChannel, async (_, msg) =>
        {
            if (socket.State != WebSocketState.Open) return;

            var emotionJson = msg.ToString();
            await db.StringSetAsync("last_emotion", emotionJson, TimeSpan.FromMinutes(1));

            var bytes = Encoding.UTF8.GetBytes(emotionJson);
            await SafeSendAsync(socket, bytes, WebSocketMessageType.Text, cancellationToken);
        });

        // 🔹 Inicia processamento das mensagens recebidas
        await ProcessIncomingMessagesAsync(socket, buffer, db, cancellationToken);
    }

    private async Task ProcessIncomingMessagesAsync(WebSocket socket, byte[] buffer, IDatabase db, CancellationToken cancellationToken)
    {
        while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            WebSocketReceiveResult? result = null;

            try
            {
                result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                LogError.Log("Erro ao receber mensagem do WebSocket", ex);
                break;
            }

            if (result == null) continue;

            switch (result.MessageType)
            {
                case WebSocketMessageType.Close:
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by client", cancellationToken);
                    break;

                case WebSocketMessageType.Text:
                    await ProcessIncomingFrameAsync(socket, buffer, result.Count, db, cancellationToken);
                    break;

                case WebSocketMessageType.Binary:
                    // Ignorado silenciosamente
                    break;
            }
        }
    }

    private async Task ProcessIncomingFrameAsync(WebSocket socket, byte[] buffer, int count, IDatabase db, CancellationToken cancellationToken)
    {
        var json = Encoding.UTF8.GetString(buffer, 0, count);

        try
        {
            using var doc = JsonDocument.Parse(json);
            _ = doc.RootElement.GetProperty("timestamp").GetString();
            _ = doc.RootElement.GetProperty("correlation_id").GetString();

            await db.ListRightPushAsync(RedisInputList, json);
        }
        catch (JsonException ex)
        {
            LogError.Log("Erro ao parsear JSON recebido", ex);
            await SendErrorAsync(socket, "invalid payload", cancellationToken);
        }
        catch (Exception ex)
        {
            LogError.Log("Erro inesperado ao processar frame", ex);
            await SendErrorAsync(socket, "internal server error", cancellationToken);
        }
    }

    private static async Task SafeSendAsync(WebSocket socket, byte[] data, WebSocketMessageType type, CancellationToken cancellationToken)
    {
        if (socket.State != WebSocketState.Open) return;

        try
        {
            await socket.SendAsync(new ArraySegment<byte>(data), type, true, cancellationToken);
        }
        catch (Exception ex)
        {
            LogError.Log("Erro ao enviar mensagem via WebSocket", ex);
        }
    }

    private static async Task SendErrorAsync(WebSocket socket, string message, CancellationToken cancellationToken)
    {
        var errorJson = JsonSerializer.Serialize(new { error = message });
        var bytes = Encoding.UTF8.GetBytes(errorJson);
        await SafeSendAsync(socket, bytes, WebSocketMessageType.Text, cancellationToken);
    }
}
