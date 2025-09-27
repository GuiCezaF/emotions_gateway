namespace emotions_gateway.middlewares
{
    public class TokenAuthMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly string _expectedToken;

        public TokenAuthMiddleware(RequestDelegate next)
        {
            _next = next;
            var token = Environment.GetEnvironmentVariable("ApiToken") ?? "";
            _expectedToken = $"Bearer {token}";
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (!context.Request.Headers.TryGetValue("Authorization", out var token) ||
                token != _expectedToken)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Unauthorized");
                return;
            }

            await _next(context);
        }
    }

    public static class TokenAuthMiddlewareExtensions
    {
        public static IApplicationBuilder UseTokenAuth(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<TokenAuthMiddleware>();
        }
    }
}
