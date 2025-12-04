using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MattEland.Jaimes.Repositories;

public static class RepositoryServiceCollectionExtensions
{
    /// <summary>
    /// Adds Jaimes repositories with an in-memory database for testing.
    /// </summary>
    public static IServiceCollection AddJaimesRepositoriesInMemory(
        this IServiceCollection services,
        string? databaseName = null)
    {
        string dbName = string.IsNullOrWhiteSpace(databaseName) ? "InMemory" : databaseName;

        services.AddDbContext<JaimesDbContext>(options =>
        {
            options.UseInMemoryDatabase(dbName, sqlOpts => { sqlOpts.EnableNullChecks(); });
        });

        return services;
    }

    /// <summary>
    /// Adds Jaimes repositories with PostgreSQL from configuration.
    /// Expects a connection string named "DefaultConnection" or provided via Aspire.
    /// </summary>
    public static IServiceCollection AddJaimesRepositories(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString("postgres-db")
                                   ?? configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Database connection string is required. Expected 'postgres-db' or 'DefaultConnection' in ConnectionStrings configuration.");
        }

        services.AddDbContext<JaimesDbContext>(options =>
        {
            options.UseNpgsql(connectionString,
                dbOpts =>
                {
                    dbOpts.MaxBatchSize(500);
                    dbOpts.EnableRetryOnFailure(maxRetryCount: 3);
                });
        });

        return services;
    }

    public static async Task InitializeDatabaseAsync(this IServiceProvider serviceProvider)
    {
        using IServiceScope scope = serviceProvider.CreateScope();
        JaimesDbContext context = scope.ServiceProvider.GetRequiredService<JaimesDbContext>();
        var loggerFactory = scope.ServiceProvider.GetService<ILoggerFactory>();
        var logger = loggerFactory?.CreateLogger("DatabaseInitialization") ??
                     NullLoggerFactory.Instance.CreateLogger("DatabaseInitialization");

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
            var message = ex.Message;
            if (message.Contains("PendingModelChangesWarning", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("pending changes", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("has pending changes", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogError(ex,
                    "Migrations cannot be applied because the model has pending changes. Add a new migration before updating the database.");
                throw new InvalidOperationException(
                    "Database model has pending changes. Add and apply a new EF Core migration (e.g. 'dotnet ef migrations add <Name>' and 'dotnet ef database update'). See https://aka.ms/efcore-docs-pending-changes.",
                    ex);
            }

            logger.LogInformation(ex,
                "MigrateAsync not supported for this provider; falling back to EnsureCreated.");
            await context.Database.EnsureCreatedAsync();
            logger.LogInformation("Database.EnsureCreatedAsync() completed.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An unexpected error occurred while running migrations.");
            throw; // Surface the error so startup fails loudly
        }
    }

    /// <summary>
    /// Initializes the database for a worker application by applying pending migrations.
    /// This method should be called after building the host and before starting the worker.
    /// Delegates to <see cref="InitializeDatabaseAsync(IServiceProvider)"/> which contains the core implementation.
    /// </summary>
    /// <param name="host">The host instance to initialize the database for.</param>
    /// <exception cref="Exception">Thrown if database initialization fails. The worker should not start in this case.</exception>
    public static async Task InitializeDatabaseAsync(this IHost host)
    {
        // Delegate to the IServiceProvider implementation which contains all the migration logic and logging
        await host.Services.InitializeDatabaseAsync();
    }
}
