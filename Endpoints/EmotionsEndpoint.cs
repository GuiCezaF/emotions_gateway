using emotions_gateway.Database;
using emotions_gateway.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Linq; 
using System;

namespace emotions_gateway.Endpoints
{
    public static class EmotionsEndpoint
    {
        public static void MapEmotions(this IEndpointRouteBuilder app)
        {
            app.MapGet("/emotions", async (HttpContext ctx, AppDbContext db) =>
            {
                ctx.Response.ContentType = "application/json";

                DateTime? start = null;
                DateTime? end = null;

                if (ctx.Request.Query.ContainsKey("start"))
                    start = DateTime.Parse(ctx.Request.Query["start"]!);

                if (ctx.Request.Query.ContainsKey("end"))
                    end = DateTime.Parse(ctx.Request.Query["end"]!);

                var query = db.emotions
                    .Include(e => e.EmotionType)
                    .AsQueryable();

                if (start.HasValue)
                {
                    var startUtc = start.Value.Date.ToUniversalTime();
                    query = query.Where(e => e.timestamp >= startUtc);
                }

                if (end.HasValue)
                {
                    var endUtc = end.Value.Date.AddDays(1).ToUniversalTime();
                    query = query.Where(e => e.timestamp < endUtc);
                }

                var result = await query
                    .GroupBy(g => g.EmotionType.name) 
                    .Select(s => new EmotionDto
                    {
                        Title = s.Key,
                        Value = s.Count()
                    })
                    .OrderByDescending(o => o.Value)
                    .ToListAsync();

                if (result == null || result.Count == 0) 
                {
                    return Results.NotFound(new { message = "Nenhuma emoção encontrada no período informado." });
                }

                return Results.Ok(result);
            });
        }
    }
}
