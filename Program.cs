using DotNetEnv;
using emotions_gateway.Database;
using emotions_gateway.Endpoints;
using emotions_gateway.Extensions;
using emotions_gateway.middlewares;
using emotions_gateway.Services;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;


var builder = WebApplication.CreateBuilder(args);
Env.Load();


builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(Environment.GetEnvironmentVariable("RedisURL"))
);

var dbUrl = Environment.GetEnvironmentVariable("DatabaseURL");

if (string.IsNullOrEmpty(dbUrl))
{
    throw new Exception("Variável de ambiente 'DatabaseURL' não encontrada!");
}

var uri = new Uri(dbUrl);
var userInfo = uri.UserInfo.Split(':');

var connectionString = $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};Username={userInfo[0]};Password={userInfo[1]}";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString)
);

var frontendUrl = Environment.GetEnvironmentVariable("FrontendURL") ?? "http://localhost:80";

builder.Services.AddCustomCors(frontendUrl);
builder.Services.AddCustomSwagger();

builder.Services.AddSingleton<ChatWebSocketService>();
builder.Services.AddSingleton<VideoWebSocketService>();

var app = builder.Build();


app.UseCors("AllowFrontend");
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseWhen(ctx => ctx.Request.Path.StartsWithSegments("/health"),
    subApp => subApp.UseTokenAuth());

app.MapHealthEndpoints();
app.UseWebSockets();
app.MapVideoWebSocketEndpoint();
app.MapChatWebSocketEndpoint();
app.MapEmotions();

app.Run();
