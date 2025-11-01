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
    public DbSet<EmotionsType> emotions_type => Set<EmotionsType>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {

        modelBuilder.Entity<Emotions>()
            .HasOne(e => e.EmotionType)
            .WithMany()
            .HasForeignKey(e => e.emotion_type_id)
            .HasPrincipalKey(t => t.id); 

        base.OnModelCreating(modelBuilder);
    }
}

