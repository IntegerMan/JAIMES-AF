using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MattEland.Jaimes.Repositories;

public static class RepositoryServiceCollectionExtensions
{
    public static IServiceCollection AddJaimesRepositories(
        this IServiceCollection services, 
        string connectionString,
        DatabaseProvider provider)
    {
        // Skip if we have a DbContext already registered
        if (services.Any(s => s.ServiceType == typeof(DbContextOptions<JaimesDbContext>)))
        {
            return services;
        }

        services.AddDbContext<JaimesDbContext>(options =>
        {
            switch (provider)
            {
                case DatabaseProvider.SqlServer:
                    options.UseSqlServer(connectionString, sqlOpts =>
                    {
                        sqlOpts.MaxBatchSize(500);
                        sqlOpts.EnableRetryOnFailure(maxRetryCount: 3);
                    });
                    break;
                case DatabaseProvider.Sqlite:
                    options.UseSqlite($"Data Source={connectionString}", dbOpts =>
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
            _ => throw new NotSupportedException($"Database provider {providerString} is not yet supported")
        };

        return services.AddJaimesRepositories(connectionString, provider);
    }

    public static async Task InitializeDatabaseAsync(this IServiceProvider serviceProvider)
    {
        using IServiceScope scope = serviceProvider.CreateScope();
        JaimesDbContext context = scope.ServiceProvider.GetRequiredService<JaimesDbContext>();
        await context.Database.EnsureCreatedAsync();
    }
}
