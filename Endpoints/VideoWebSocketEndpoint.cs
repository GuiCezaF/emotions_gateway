using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace emotions_gateway.Endpoints;

public static class VideoWebSocketEndpoint
{
    public static void MapVideoWebSocketEndpoint(this IEndpointRouteBuilder app)
    {
        app.Map("/emotions/video", async (HttpContext ctx) =>
        {
            if (!ctx.WebSockets.IsWebSocketRequest)
            {
                ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
                await ctx.Response.WriteAsync("WebSocket request expected.");
                return;
            }

            using var socket = await ctx.WebSockets.AcceptWebSocketAsync();
            var buffer = new byte[256 * 1024];
            var closeReceived = false;

            while (!closeReceived && socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ctx.RequestAborted);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    closeReceived = true;
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by client", ctx.RequestAborted);
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    await HandleTextMessage(socket, buffer, result.Count, ctx);
                }
                else if (result.MessageType == WebSocketMessageType.Binary)
                {
                    Console.WriteLine($"[WS] Mensagem binária inesperada ({result.Count} bytes) – ignorada.");
                }
            }
        });
    }

    private static async Task HandleTextMessage(WebSocket socket, byte[] buffer, int count, HttpContext ctx)
    {
        var json = Encoding.UTF8.GetString(buffer, 0, count);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var timestamp = root.GetProperty("timestamp").GetString();
            var base64 = root.GetProperty("frame").GetString();

            Console.WriteLine($"[WS] Timestamp: {timestamp}, frame size: {base64?.Length ?? 0} chars");

            var echoObj = new { echo = "received", timestamp };
            var echoJson = JsonSerializer.Serialize(echoObj);
            var echoBytes = Encoding.UTF8.GetBytes(echoJson);

            await socket.SendAsync(new ArraySegment<byte>(echoBytes), WebSocketMessageType.Text, true, ctx.RequestAborted);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WS] Erro ao parsear JSON: {ex.Message}");
            var errMsg = JsonSerializer.Serialize(new { error = "invalid payload" });
            var errBytes = Encoding.UTF8.GetBytes(errMsg);
            await socket.SendAsync(new ArraySegment<byte>(errBytes), WebSocketMessageType.Text, true, ctx.RequestAborted);
        }
    }
}
