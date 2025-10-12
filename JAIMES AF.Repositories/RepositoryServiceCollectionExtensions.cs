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
                    options.UseInMemoryDatabase("InMemory", sqlOpts =>
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
        string connectionString = configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("DefaultConnection is required");
        string providerString = configuration["DatabaseProvider"] ?? throw new InvalidOperationException("DatabaseProvider is required");
        
        DatabaseProvider provider = providerString.ToLowerInvariant() switch
        {
            "sqlserver" => DatabaseProvider.SqlServer,
            "sqlite" => DatabaseProvider.Sqlite,
            "inmemory" => DatabaseProvider.InMemory,
            _ => throw new NotSupportedException($"Database provider {providerString} is not yet supported")
        };

        return services.AddJaimesRepositories(provider, connectionString);
    }

    public static async Task InitializeDatabaseAsync(this IServiceProvider serviceProvider)
    {
        using IServiceScope scope = serviceProvider.CreateScope();
        JaimesDbContext context = scope.ServiceProvider.GetRequiredService<JaimesDbContext>();
        await context.Database.EnsureCreatedAsync();
    }
}
