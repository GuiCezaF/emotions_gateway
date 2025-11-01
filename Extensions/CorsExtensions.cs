namespace emotions_gateway.Extensions;

public static class CorsExtensions
{
    public static void AddCustomCors(this IServiceCollection services, string frontendUrl)
    {
        services.AddCors(options =>
        {
            options.AddPolicy("AllowFrontend", policy =>
            {
                policy.WithOrigins(frontendUrl)
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
            });
        });
    }
}