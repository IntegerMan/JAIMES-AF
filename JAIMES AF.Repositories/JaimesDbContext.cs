using Microsoft.EntityFrameworkCore;
using MattEland.Jaimes.Repositories.Entities;

namespace MattEland.Jaimes.Repositories;

public class JaimesDbContext(DbContextOptions<JaimesDbContext> options) : DbContext(options)
{
    public DbSet<Game> Games { get; set; }
    public DbSet<Message> Messages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Game>(entity =>
        {
            entity.HasKey(g => g.Id);
            entity.Property(g => g.RulesetId).IsRequired();
            entity.Property(g => g.ScenarioId).IsRequired();
            entity.Property(g => g.PlayerId).IsRequired();
            entity.Property(g => g.CreatedAt).IsRequired();
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.Property(m => m.GameId).IsRequired();
            entity.Property(m => m.Text).IsRequired();
            entity.Property(m => m.CreatedAt).IsRequired();
            
            entity.HasOne(m => m.Game)
                .WithMany(g => g.Messages)
                .HasForeignKey(m => m.GameId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
