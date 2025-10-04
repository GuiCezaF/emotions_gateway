using DotNetEnv;
using emotions_gateway.Endpoints;
using emotions_gateway.Extensions;
using emotions_gateway.middlewares;
using StackExchange.Redis;


var builder = WebApplication.CreateBuilder(args);
Env.Load();


builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect("redis:6379,abortConnect=false") // TODO: colocar no .env
);


builder.Services.AddCustomCors();
builder.Services.AddCustomSwagger();

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

app.Run();
