using emotions_gateway.Services;
using StackExchange.Redis;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace emotions_gateway.Endpoints;

public static class ChatWebsocketEndpoint
{
    public static void MapChatWebSocketEndpoint(this IEndpointRouteBuilder app)
    {
        app.Map("/emotions/chat", async (HttpContext ctx, IConnectionMultiplexer redis, ChatWebSocketService chatService) =>
        {
            if (!ctx.WebSockets.IsWebSocketRequest)
            {
                ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
                await ctx.Response.WriteAsync("WebSocket request expected.");
                return;
            }

            using var socket = await ctx.WebSockets.AcceptWebSocketAsync();
            var cancellationToken = ctx.RequestAborted;
            var buffer = new byte[4 * 1024]; // 4 KB

            while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", cancellationToken);
                    break;
                }

                var receivedJson = Encoding.UTF8.GetString(buffer, 0, result.Count);

                var (responseJson, isError) = await chatService.ProcessMessageAsync(receivedJson, cancellationToken);

                var responseBytes = Encoding.UTF8.GetBytes(responseJson);
                await socket.SendAsync(new ArraySegment<byte>(responseBytes), WebSocketMessageType.Text, true, cancellationToken);
            }
        });
    }
}
