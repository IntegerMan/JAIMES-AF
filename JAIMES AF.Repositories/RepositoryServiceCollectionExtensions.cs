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
        // If the test project or other caller has already registered the DbContext (e.g. to use InMemory),
        // avoid registering another provider which causes EF Core to throw when multiple providers are registered.
        // Check for common DbContext registrations and skip adding if present.
        if (services.Any(sd => sd.ServiceType == typeof(DbContextOptions<JaimesDbContext>)
                               || sd.ServiceType == typeof(JaimesDbContext)
                               || sd.ServiceType == typeof(IDbContextFactory<JaimesDbContext>)))
        {
            return services;
        }

        // Use the overload that provides the IServiceProvider so we can defer determining which provider
        // to configure until the DbContext is actually created. This allows test configuration applied by
        // WebApplicationFactory to be honored and avoids registering multiple providers during service registration.
        services.AddDbContext<JaimesDbContext>((serviceProvider, options) =>
        {
            // Allow short-circuiting via environment variable or configuration key so tests can opt-out
            // of the application's DB registration entirely.
            var envSkip = Environment.GetEnvironmentVariable("SkipDatabaseRegistration");
            if (!string.IsNullOrWhiteSpace(envSkip) && bool.TryParse(envSkip, out var envSkipVal) && envSkipVal)
            {
                return;
            }

            var config = serviceProvider.GetService<IConfiguration>();
            var skipValue = config?["SkipDatabaseRegistration"];
            if (!string.IsNullOrWhiteSpace(skipValue) && bool.TryParse(skipValue, out var skip) && skip)
            {
                return;
            }

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
        // Allow tests to completely skip registering the application's database provider so tests may
        // register their own in-memory provider without causing EF Core to have multiple providers.
        var envSkip = Environment.GetEnvironmentVariable("SkipDatabaseRegistration");
        if (!string.IsNullOrWhiteSpace(envSkip) && bool.TryParse(envSkip, out var envSkipVal) && envSkipVal)
        {
            return services;
        }

        var skipValue = configuration["SkipDatabaseRegistration"];
        if (!string.IsNullOrWhiteSpace(skipValue) && bool.TryParse(skipValue, out var skip) && skip)
        {
            return services;
        }

        // Support both top-level keys and a "Jaimes" section so test and production configurations both work.
        // Determine provider first so we only require a connection string for providers that need it.
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

        string? connectionString = null;

        if (provider != DatabaseProvider.InMemory)
        {
            // Try the standard connection string lookup first, then fall back to possible alternate locations.
            connectionString = configuration.GetConnectionString("DefaultConnection")
                               ?? configuration["ConnectionStrings:DefaultConnection"]
                               ?? configuration["Jaimes:ConnectionStrings:DefaultConnection"];

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("DefaultConnection is required for non-InMemory database providers");
            }
        }

        return services.AddJaimesRepositories(provider, connectionString);
    }

    public static async Task InitializeDatabaseAsync(this IServiceProvider serviceProvider)
    {
        using IServiceScope scope = serviceProvider.CreateScope();
        JaimesDbContext context = scope.ServiceProvider.GetRequiredService<JaimesDbContext>();
        await context.Database.EnsureCreatedAsync();
    }
}
