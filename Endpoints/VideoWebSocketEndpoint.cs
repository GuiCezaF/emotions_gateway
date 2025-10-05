using StackExchange.Redis;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace emotions_gateway.Endpoints;

public static class VideoWebSocketEndpoint
{
    private const int BufferSize = 256 * 1024; // 256 KB
    private const string RedisChannel = "emotion_results";
    private const string RedisInputList = "emotion_frames";
    private const string RedisOutputList = "emotion_results";

    public static void MapVideoWebSocketEndpoint(this IEndpointRouteBuilder app)
    {
        app.Map("/emotions/video", async (HttpContext ctx, IConnectionMultiplexer redis) =>
        {
            if (!ctx.WebSockets.IsWebSocketRequest)
            {
                ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
                await ctx.Response.WriteAsync("WebSocket request expected.");
                return;
            }

            using var socket = await ctx.WebSockets.AcceptWebSocketAsync();
            var db = redis.GetDatabase();
            var sub = redis.GetSubscriber();
            var cancellationToken = ctx.RequestAborted;
            var buffer = new byte[BufferSize];

            // Assina canal Redis para enviar mensagens ao cliente
            await sub.SubscribeAsync(RedisChannel, async (_, msg) =>
            {
                if (socket.State == WebSocketState.Open)
                {
                    var bytes = Encoding.UTF8.GetBytes(msg.ToString());
                    await SafeSendAsync(socket, bytes, WebSocketMessageType.Text, cancellationToken);
                }
            });

            // Inicia loop que envia resultados do Redis para o cliente
            _ = Task.Run(() => ProcessRedisOutputAsync(db, socket, cancellationToken), cancellationToken);

            // Loop principal: recebe frames do cliente
            await ProcessIncomingMessagesAsync(socket, buffer, db, cancellationToken);
        });
    }

    private static async Task ProcessRedisOutputAsync(IDatabase db, WebSocket socket, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            try
            {
                var result = await db.ListLeftPopAsync(RedisOutputList);
                if (!result.IsNullOrEmpty)
                {
                    var bytes = Encoding.UTF8.GetBytes(result.ToString());
                    await SafeSendAsync(socket, bytes, WebSocketMessageType.Text, cancellationToken);
                }
                else
                {
                    await Task.Delay(200, cancellationToken); // evita busy loop
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                LogError("Erro ao consumir resultados do Redis", ex);
                await Task.Delay(1000, cancellationToken);
            }
        }
    }

    private static async Task ProcessIncomingMessagesAsync(WebSocket socket, byte[] buffer, IDatabase db, CancellationToken cancellationToken)
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
                LogError("Erro ao receber mensagem do WebSocket", ex);
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

    private static async Task ProcessIncomingFrameAsync(WebSocket socket, byte[] buffer, int count, IDatabase db, CancellationToken cancellationToken)
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
            LogError("Erro ao parsear JSON recebido", ex);
            await SendErrorAsync(socket, "invalid payload", cancellationToken);
        }
        catch (Exception ex)
        {
            LogError("Erro inesperado ao processar frame", ex);
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
            LogError("Erro ao enviar mensagem via WebSocket", ex);
        }
    }

    private static async Task SendErrorAsync(WebSocket socket, string message, CancellationToken cancellationToken)
    {
        var errorJson = JsonSerializer.Serialize(new { error = message });
        var bytes = Encoding.UTF8.GetBytes(errorJson);
        await SafeSendAsync(socket, bytes, WebSocketMessageType.Text, cancellationToken);
    }

    private static void LogError(string context, Exception ex)
    {
        Console.Error.WriteLine($"[ERRO] {context}: {ex.Message}");
    }
}

