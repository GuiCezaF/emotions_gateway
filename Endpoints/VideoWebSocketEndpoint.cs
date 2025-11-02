using emotions_gateway.Services;
using StackExchange.Redis;
using System.Net.WebSockets;

namespace emotions_gateway.Endpoints;

public static class VideoWebSocketEndpoint
{
    public static void MapVideoWebSocketEndpoint(this IEndpointRouteBuilder app)
    {
        app.Map("/emotions/video", async (HttpContext ctx, IConnectionMultiplexer redis, VideoWebSocketService videoService) =>
        {
            if (!ctx.WebSockets.IsWebSocketRequest)
            {
                ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
                await ctx.Response.WriteAsync("WebSocket request expected.");
                return;
            }

            using var socket = await ctx.WebSockets.AcceptWebSocketAsync();
            var cancellationToken = ctx.RequestAborted;

            await videoService.HandleConnectionAsync(socket, cancellationToken);
        });
    }
}
