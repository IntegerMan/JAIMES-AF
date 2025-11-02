using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
        var loggerFactory = scope.ServiceProvider.GetService<ILoggerFactory>();
        var logger = loggerFactory?.CreateLogger("DatabaseInitialization") ?? NullLoggerFactory.Instance.CreateLogger("DatabaseInitialization");

        try
        {
            string providerName = context.Database.ProviderName ?? "unknown";
            logger.LogInformation("Starting database initialization. EF provider: {Provider}", providerName);

            IEnumerable<string> appliedMigrations = Enumerable.Empty<string>();
            IEnumerable<string> pendingMigrations = Enumerable.Empty<string>();

            try
            {
                appliedMigrations = context.Database.GetAppliedMigrations().ToArray();
                pendingMigrations = context.Database.GetPendingMigrations().ToArray();
                logger.LogInformation("Applied migrations count: {AppliedCount}", appliedMigrations.Count());
                logger.LogInformation("Pending migrations count: {PendingCount}", pendingMigrations.Count());

                foreach (var m in pendingMigrations)
                {
                    logger.LogInformation("Pending migration: {Migration}", m);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to enumerate applied/pending migrations. Continuing to migration step.");
            }

            try
            {
                await context.Database.MigrateAsync();
                logger.LogInformation("Database.MigrateAsync() completed successfully.");

                try
                {
                    var appliedAfter = context.Database.GetAppliedMigrations().ToArray();
                    logger.LogInformation("Applied migrations after migrate: {Count}", appliedAfter.Length);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to enumerate applied migrations after migrate.");
                }
            }
            catch (InvalidOperationException ex)
            {
                // If the exception indicates pending model changes relative to migrations, surface a clear error
                // so the developer knows to add a migration. For other InvalidOperationException cases (e.g., provider
                // doesn't support Migrate), fall back to EnsureCreated.
                var message = ex.Message ?? string.Empty;
                if (message.Contains("PendingModelChangesWarning", StringComparison.OrdinalIgnoreCase) ||
                    message.Contains("pending changes", StringComparison.OrdinalIgnoreCase) ||
                    message.Contains("has pending changes", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogError(ex, "Migrations cannot be applied because the model has pending changes. Add a new migration before updating the database.");
                    throw new InvalidOperationException("Database model has pending changes. Add and apply a new EF Core migration (e.g. 'dotnet ef migrations add <Name>' and 'dotnet ef database update'). See https://aka.ms/efcore-docs-pending-changes.", ex);
                }

                logger.LogInformation(ex, "MigrateAsync not supported for this provider; falling back to EnsureCreated.");
                await context.Database.EnsureCreatedAsync();
                logger.LogInformation("Database.EnsureCreatedAsync() completed.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An unexpected error occurred while running migrations.");
                throw; // Surface the error so startup fails loudly
            }
        }
        finally
        {
            // Nothing specific to dispose here beyond the scope
        }
    }
}
