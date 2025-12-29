namespace MattEland.Jaimes.Repositories;

public static class RepositoryServiceCollectionExtensions
{
    /// <summary>
    /// Adds Jaimes repositories with an in-memory database for testing.
    /// Registers both DbContext (for direct injection) and IDbContextFactory (for worker service tests).
    /// </summary>
    public static IServiceCollection AddJaimesRepositoriesInMemory(
        this IServiceCollection services,
        string? databaseName = null)
    {
        string dbName = string.IsNullOrWhiteSpace(databaseName) ? "InMemory" : databaseName;

        // Register DbContext for direct injection
        services.AddDbContext<JaimesDbContext>(options =>
        {
            options.UseInMemoryDatabase(dbName, sqlOpts => { sqlOpts.EnableNullChecks(); });
        });

        // Register DbContextFactory for worker service tests
        services.AddDbContextFactory<JaimesDbContext>(options =>
        {
            options.UseInMemoryDatabase(dbName, sqlOpts => { sqlOpts.EnableNullChecks(); });
        });

        return services;
    }

    /// <summary>
    /// Adds Jaimes repositories with PostgreSQL from configuration.
    /// Expects a connection string named "DefaultConnection" or provided via Aspire.
    /// Registers both DbContext (for direct injection) and IDbContextFactory (for worker services).
    /// </summary>
    public static IServiceCollection AddJaimesRepositories(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString("postgres-db")
                                   ?? configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "Database connection string is required. Expected 'postgres-db' or 'DefaultConnection' in ConnectionStrings configuration.");

        // Register DbContextFactory for all services (API and worker services)
        // Using AddPooledDbContextFactory provides efficient connection pooling for all contexts
        services.AddPooledDbContextFactory<JaimesDbContext>(options =>
        {
            options.UseNpgsql(connectionString,
                dbOpts =>
                {
                    dbOpts.UseVector(); // Enable pgvector support
                    dbOpts.MaxBatchSize(500);
                    dbOpts.EnableRetryOnFailure(3);
                });
        });

        return services;
    }

    /// <summary>
    /// Waits for database migrations to be applied by the migration worker.
    /// This method checks for pending migrations and waits for them to be applied.
    /// It does NOT apply migrations itself - that is handled by the migration worker.
    /// </summary>
    public static async Task WaitForMigrationsAsync(this IServiceProvider serviceProvider)
    {
        using IServiceScope scope = serviceProvider.CreateScope();
        IDbContextFactory<JaimesDbContext> factory =
            scope.ServiceProvider.GetRequiredService<IDbContextFactory<JaimesDbContext>>();
        using JaimesDbContext context = await factory.CreateDbContextAsync();
        ILoggerFactory? loggerFactory = scope.ServiceProvider.GetService<ILoggerFactory>();
        ILogger logger = loggerFactory?.CreateLogger("DatabaseInitialization") ??
                         NullLoggerFactory.Instance.CreateLogger("DatabaseInitialization");

        string providerName = context.Database.ProviderName ?? "unknown";
        logger.LogInformation("Waiting for database migrations. EF provider: {Provider}", providerName);

        const int maxWaitSeconds = 300; // 5 minutes max wait
        const int checkIntervalMs = 1000; // Check every second
        int waitedSeconds = 0;

        while (waitedSeconds < maxWaitSeconds)
        {
            try
            {
                string[] pendingMigrations = context.Database.GetPendingMigrations().ToArray();
                
                if (pendingMigrations.Length == 0)
                {
                    string[] appliedMigrations = context.Database.GetAppliedMigrations().ToArray();
                    logger.LogInformation(
                        "All migrations applied. Applied migrations count: {AppliedCount}. Proceeding with startup.",
                        appliedMigrations.Length);
                    return;
                }

                if (waitedSeconds == 0)
                {
                    logger.LogInformation(
                        "Found {PendingCount} pending migrations. Waiting for migration worker to apply them...",
                        pendingMigrations.Length);
                    foreach (string m in pendingMigrations)
                    {
                        logger.LogInformation("Pending migration: {Migration}", m);
                    }
                }

                await Task.Delay(checkIntervalMs);
                waitedSeconds += checkIntervalMs / 1000;

                // Log progress every 10 seconds
                if (waitedSeconds % 10 == 0)
                {
                    logger.LogInformation(
                        "Still waiting for migrations to be applied... ({WaitSeconds}s / {MaxWaitSeconds}s)",
                        waitedSeconds,
                        maxWaitSeconds);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Error checking migration status (attempt {Attempt}). Retrying...",
                    waitedSeconds);
                await Task.Delay(checkIntervalMs);
                waitedSeconds += checkIntervalMs / 1000;
            }
        }

        // Final check
        try
        {
            string[] finalPending = context.Database.GetPendingMigrations().ToArray();
            if (finalPending.Length > 0)
            {
                logger.LogError(
                    "Timeout waiting for migrations. {PendingCount} migrations still pending after {MaxWaitSeconds} seconds.",
                    finalPending.Length,
                    maxWaitSeconds);
                throw new InvalidOperationException(
                    $"Database migrations were not applied within {maxWaitSeconds} seconds. " +
                    $"The migration worker may not be running or may have failed. " +
                    $"Pending migrations: {string.Join(", ", finalPending)}");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to verify migration status after timeout.");
            throw;
        }
    }

    /// <summary>
    /// Applies database migrations. This should only be called by the migration worker.
    /// </summary>
    public static async Task ApplyMigrationsAsync(this IServiceProvider serviceProvider)
    {
        using IServiceScope scope = serviceProvider.CreateScope();
        IDbContextFactory<JaimesDbContext> factory =
            scope.ServiceProvider.GetRequiredService<IDbContextFactory<JaimesDbContext>>();
        using JaimesDbContext context = await factory.CreateDbContextAsync();
        ILoggerFactory? loggerFactory = scope.ServiceProvider.GetService<ILoggerFactory>();
        ILogger logger = loggerFactory?.CreateLogger("DatabaseInitialization") ??
                         NullLoggerFactory.Instance.CreateLogger("DatabaseInitialization");

        string providerName = context.Database.ProviderName ?? "unknown";
        logger.LogInformation("Applying database migrations. EF provider: {Provider}", providerName);

        IEnumerable<string> appliedMigrations = Enumerable.Empty<string>();
        IEnumerable<string> pendingMigrations = Enumerable.Empty<string>();

        try
        {
            appliedMigrations = context.Database.GetAppliedMigrations().ToArray();
            pendingMigrations = context.Database.GetPendingMigrations().ToArray();
            logger.LogInformation("Applied migrations count: {AppliedCount}", appliedMigrations.Count());
            logger.LogInformation("Pending migrations count: {PendingCount}", pendingMigrations.Count());

            foreach (string m in pendingMigrations) logger.LogInformation("Pending migration: {Migration}", m);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to enumerate applied/pending migrations. Continuing to migration step.");
        }

        if (!pendingMigrations.Any())
        {
            logger.LogInformation("No pending migrations. Database is up to date.");
            return;
        }

        try
        {
            await context.Database.MigrateAsync();
            logger.LogInformation("Database.MigrateAsync() completed successfully.");

            try
            {
                string[] appliedAfter = context.Database.GetAppliedMigrations().ToArray();
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
            string message = ex.Message;
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
    /// Waits for database migrations to be applied by the migration worker.
    /// This method should be called after building the host and before starting the worker/API.
    /// </summary>
    public static async Task WaitForMigrationsAsync(this IHost host)
    {
        await host.Services.WaitForMigrationsAsync();
    }

    /// <summary>
    /// Applies database migrations. This should only be called by the migration worker.
    /// </summary>
    public static async Task ApplyMigrationsAsync(this IHost host)
    {
        await host.Services.ApplyMigrationsAsync();
    }

    /// <summary>
    /// Applies database migrations. This is used by tests and the migration worker.
    /// For production code, use WaitForMigrationsAsync instead.
    /// </summary>
    /// <param name="serviceProvider">The service provider to use for database operations.</param>
    [Obsolete("Use ApplyMigrationsAsync for tests or WaitForMigrationsAsync for production. This method is kept for backward compatibility.")]
    public static async Task InitializeDatabaseAsync(this IServiceProvider serviceProvider)
    {
        // For backward compatibility, delegate to ApplyMigrationsAsync (tests need to actually apply migrations)
        await serviceProvider.ApplyMigrationsAsync();
    }

    /// <summary>
    /// Initializes the database for a worker application by applying pending migrations.
    /// This method should be called after building the host and before starting the worker.
    /// Delegates to <see cref="WaitForMigrationsAsync(IServiceProvider)"/> which waits for migrations to be applied.
    /// </summary>
    /// <param name="host">The host instance to initialize the database for.</param>
    /// <exception cref="Exception">Thrown if database initialization fails. The worker should not start in this case.</exception>
    [Obsolete("Use WaitForMigrationsAsync instead. This method is kept for backward compatibility but will be removed.")]
    public static async Task InitializeDatabaseAsync(this IHost host)
    {
        // For backward compatibility, delegate to WaitForMigrationsAsync
        await host.WaitForMigrationsAsync();
    }
}