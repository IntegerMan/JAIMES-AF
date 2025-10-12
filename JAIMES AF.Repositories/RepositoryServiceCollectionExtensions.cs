using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MattEland.Jaimes.Repositories;

public static class RepositoryServiceCollectionExtensions
{
    public static IServiceCollection AddJaimesRepositories(this IServiceCollection services, string databasePath = "jaimes.db")
    {
        services.AddDbContext<JaimesDbContext>(options =>
            options.UseSqlite($"Data Source={databasePath}", dbOpts =>
            {
                dbOpts.MaxBatchSize(500);
            }));

        return services;
    }

    public static async Task InitializeDatabaseAsync(this IServiceProvider serviceProvider)
    {
        using IServiceScope scope = serviceProvider.CreateScope();
        JaimesDbContext context = scope.ServiceProvider.GetRequiredService<JaimesDbContext>();
        await context.Database.EnsureCreatedAsync();
    }
}
