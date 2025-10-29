using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MattEland.Jaimes.Repositories;

public static class RepositoryServiceCollectionExtensions
{
    public static IServiceCollection AddJaimesRepositories(this IServiceCollection services, DatabaseProvider provider, string? connectionString = null)
    {
        services.AddDbContext<JaimesDbContext>(options =>
        {
            switch (provider)
            {
                case DatabaseProvider.InMemory:
                    string dbName = string.IsNullOrWhiteSpace(connectionString) ? "InMemory" : connectionString;
                    options.UseInMemoryDatabase(dbName, sqlOpts =>
                    {
                        sqlOpts.EnableNullChecks();
                    });
                    break;
                case DatabaseProvider.SqlServer:
                    options.UseSqlServer(connectionString, sqlOpts =>
                    {
                        sqlOpts.MaxBatchSize(500);
                        sqlOpts.EnableRetryOnFailure(maxRetryCount: 3);
                    });
                    break;
                case DatabaseProvider.Sqlite:
                    options.UseSqlite(connectionString, dbOpts =>
                    {
                        dbOpts.MaxBatchSize(500);
                    });
                    break;
                default:
                    throw new NotSupportedException($"Database provider {provider} is not supported for adding repositories");
            }
        });

        return services;
    }

    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    public static IServiceCollection AddJaimesRepositories(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string? providerString = configuration["DatabaseProvider"]
                                 ?? configuration["Jaimes:DatabaseProvider"];

        if (string.IsNullOrWhiteSpace(providerString))
        {
            throw new InvalidOperationException("DatabaseProvider is required");
        }

        DatabaseProvider provider = providerString.ToLowerInvariant() switch
        {
            "sqlserver" => DatabaseProvider.SqlServer,
            "sqlite" => DatabaseProvider.Sqlite,
            "inmemory" => DatabaseProvider.InMemory,
            _ => throw new NotSupportedException($"Database provider {providerString} is not yet supported")
        };

        string? connectionString = configuration.GetConnectionString("DefaultConnection")
                           ?? configuration["ConnectionStrings:DefaultConnection"]
                           ?? configuration["Jaimes:ConnectionStrings:DefaultConnection"];

        if (provider != DatabaseProvider.InMemory && string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("DefaultConnection is required for non-InMemory database providers");
        }

        return services.AddJaimesRepositories(provider, connectionString);
    }

    public static async Task InitializeDatabaseAsync(this IServiceProvider serviceProvider)
    {
        using IServiceScope scope = serviceProvider.CreateScope();
        JaimesDbContext context = scope.ServiceProvider.GetRequiredService<JaimesDbContext>();

        // Prefer applying EF Core migrations on startup for relational databases.
        // If migrations aren't supported (e.g. InMemory provider) fall back to EnsureCreated.
        try
        {
            await context.Database.MigrateAsync();
        }
        catch (InvalidOperationException)
        {
            // In-memory provider and some others will throw when Migrate is used; fall back.
            await context.Database.EnsureCreatedAsync();
        }
    }
}
