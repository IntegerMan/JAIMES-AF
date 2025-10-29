using Microsoft.EntityFrameworkCore;
using MattEland.Jaimes.Repositories.Entities;

namespace MattEland.Jaimes.Repositories;

public class JaimesDbContext(DbContextOptions<JaimesDbContext> options) : DbContext(options)
{
    public DbSet<Game> Games { get; set; }
    public DbSet<Message> Messages { get; set; }
    public DbSet<Player> Players { get; set; }
    public DbSet<Scenario> Scenarios { get; set; }
    public DbSet<Ruleset> Rulesets { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Ruleset>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Id).IsRequired();
            entity.Property(r => r.Name).IsRequired();
        });

        modelBuilder.Entity<Player>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Id).IsRequired();
            entity.Property(p => p.RulesetId).IsRequired();
            
            entity.HasOne(p => p.Ruleset)
                .WithMany(r => r.Players)
                .HasForeignKey(p => p.RulesetId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Scenario>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Id).IsRequired();
            entity.Property(s => s.RulesetId).IsRequired();
            
            entity.HasOne(s => s.Ruleset)
                .WithMany(r => r.Scenarios)
                .HasForeignKey(s => s.RulesetId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Game>(entity =>
        {
            entity.HasKey(g => g.Id);
            entity.Property(g => g.RulesetId).IsRequired();
            entity.Property(g => g.ScenarioId).IsRequired();
            entity.Property(g => g.PlayerId).IsRequired();
            entity.Property(g => g.CreatedAt).IsRequired();
            
            entity.HasOne(g => g.Ruleset)
                .WithMany(r => r.Games)
                .HasForeignKey(g => g.RulesetId)
                .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasOne(g => g.Scenario)
                .WithMany(s => s.Games)
                .HasForeignKey(g => g.ScenarioId)
                .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasOne(g => g.Player)
                .WithMany(p => p.Games)
                .HasForeignKey(g => g.PlayerId)
                .OnDelete(DeleteBehavior.Restrict);
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

        // Seed data - use lowercase ids for new defaults
        modelBuilder.Entity<Ruleset>().HasData(
            new Ruleset { Id = "dnd5e", Name = "Dungeons and Dragons5th Edition" }
        );

        modelBuilder.Entity<Player>().HasData(
            new Player { Id = "emcee", RulesetId = "dnd5e", Description = "Default player" }
        );

        modelBuilder.Entity<Scenario>().HasData(
            new Scenario { Id = "islandTest", RulesetId = "dnd5e", Description = "Island test scenario" }
        );
    }
}
