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
            var buffer = new byte[BufferSize];
            var db = redis.GetDatabase();
            var sub = redis.GetSubscriber();
            var cancellationToken = ctx.RequestAborted;

            await sub.SubscribeAsync(RedisChannel, async (_, msg) =>
            {
                if (socket.State == WebSocketState.Open)
                {
                    var bytes = Encoding.UTF8.GetBytes(msg.ToString());
                    await SafeSendAsync(socket, bytes, WebSocketMessageType.Text, cancellationToken);
                }
            });

            _ = Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
                {
                    try
                    {
                        
                        var result = await db.ListLeftPopAsync(RedisOutputList);
                        if (!result.IsNullOrEmpty)
                        {
                            var json = result.ToString();
                            var bytes = Encoding.UTF8.GetBytes(json);
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
                        Console.WriteLine($"[WS] Erro ao consumir lista do Redis: {ex.Message}");
                        await Task.Delay(1000, cancellationToken);
                    }
                }
            }, cancellationToken);

            // Loop principal de recepção (frames do cliente)
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
                    Console.WriteLine($"[WS] Erro na recepção: {ex.Message}");
                    break;
                }

                if (result == null) continue;

                switch (result.MessageType)
                {
                    case WebSocketMessageType.Close:
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by client", cancellationToken);
                        break;

                    case WebSocketMessageType.Text:
                        await HandleTextMessage(socket, buffer, result.Count, db, cancellationToken);
                        break;

                    case WebSocketMessageType.Binary:
                        Console.WriteLine($"[WS] Mensagem binária inesperada ({result.Count} bytes) – ignorada.");
                        break;
                }
            }
        });
    }

    private static async Task HandleTextMessage(WebSocket socket, byte[] buffer, int count, IDatabase db, CancellationToken cancellationToken)
    {
        var json = Encoding.UTF8.GetString(buffer, 0, count);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var timestamp = root.GetProperty("timestamp").GetString();
            var correlationId = root.GetProperty("correlation_id").GetString();

            Console.WriteLine($"[WS] Recebido frame do usuário {correlationId} em {timestamp}");

            await db.ListRightPushAsync(RedisInputList, json);
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"[WS] Erro ao parsear JSON: {ex.Message}");
            await SendError(socket, "invalid payload", cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WS] Erro inesperado: {ex.Message}");
            await SendError(socket, "internal server error", cancellationToken);
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
            Console.WriteLine($"[WS] Erro ao enviar mensagem: {ex.Message}");
        }
    }

    private static async Task SendError(WebSocket socket, string message, CancellationToken cancellationToken)
    {
        var errJson = JsonSerializer.Serialize(new { error = message });
        await SafeSendAsync(socket, Encoding.UTF8.GetBytes(errJson), WebSocketMessageType.Text, cancellationToken);
    }
}
