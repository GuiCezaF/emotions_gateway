using emotions_gateway.Models;
using Microsoft.EntityFrameworkCore;

namespace emotions_gateway.Database;
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Emotions> emotions => Set<Emotions>();
}
