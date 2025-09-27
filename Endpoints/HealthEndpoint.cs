namespace emotions_gateway.Endpoints
{
    public static class HealthEndpoint
    {
        public static void MapHealthEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }))
               .WithName("HealthCheck")
               .WithOpenApi();
        }
    }
}
