using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MattEland.Jaimes.Repositories;

public enum DatabaseProvider
{
    Sqlite,
    SqlServer
}

public static class RepositoryServiceCollectionExtensions
{
    public static IServiceCollection AddJaimesRepositories(
        this IServiceCollection services, 
        string connectionString = "jaimes.db",
        DatabaseProvider provider = DatabaseProvider.Sqlite)
    {
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
                default:
                    options.UseSqlite($"Data Source={connectionString}", dbOpts =>
                    {
                        dbOpts.MaxBatchSize(500);
                    });
                    break;
            }
        });

        return services;
    }

    public static IServiceCollection AddJaimesRepositories(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection") ?? "jaimes.db";
        var providerString = configuration["DatabaseProvider"] ?? "Sqlite";
        
        DatabaseProvider provider = providerString.ToLowerInvariant() switch
        {
            "sqlserver" => DatabaseProvider.SqlServer,
            "sqlite" => DatabaseProvider.Sqlite,
            _ => DatabaseProvider.Sqlite
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
