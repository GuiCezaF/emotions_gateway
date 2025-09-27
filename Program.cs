using DotNetEnv;
using emotions_gateway.Endpoints;
using emotions_gateway.Extensions;
using emotions_gateway.middlewares;


var builder = WebApplication.CreateBuilder(args);
Env.Load();


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
