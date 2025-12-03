using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MattEland.Jaimes.Repositories;

/// <summary>
/// Design-time factory for EF Core migrations.
/// This allows the EF Core tools to create migrations without requiring a runtime connection string.
/// </summary>
public class JaimesDbContextFactory : IDesignTimeDbContextFactory<JaimesDbContext>
{
    public JaimesDbContext CreateDbContext(string[] args)
    {
        // For design-time, use a default PostgreSQL connection string
        // This will be overridden at runtime by the actual configuration
        DbContextOptionsBuilder<JaimesDbContext> optionsBuilder = new();
        optionsBuilder.UseNpgsql("Host=localhost;Database=postgres;Username=postgres;Password=postgres");

        return new JaimesDbContext(optionsBuilder.Options);
    }
}

