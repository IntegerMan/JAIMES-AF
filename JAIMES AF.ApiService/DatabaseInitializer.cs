namespace MattEland.Jaimes.ApiService;

public class DatabaseInitializer(ActivitySource activitySource, ILogger<DatabaseInitializer> logger)
{
    public async Task InitializeAsync(WebApplication app)
    {
        using Activity? activity = activitySource.StartActivity("DatabaseInitialization", ActivityKind.Internal);
        activity?.SetTag("component", "database-init");

        bool skipDbInit = app.Configuration.GetValue<bool>("SkipDatabaseInitialization");
        activity?.SetTag("db.init.skip_config", skipDbInit);

        if (skipDbInit)
        {
            logger?.LogInformation(
                "Database initialization skipped via configuration (SkipDatabaseInitialization=true).");
            activity?.SetTag("db.initialization.skipped", true);
        }
        else
        {
            logger?.LogInformation("Database initialization running now.");

            using Activity? migrateActivity = activitySource.StartActivity("ApplyMigrations", ActivityKind.Internal);
            migrateActivity?.SetTag("db.operation", "migrate");

            try
            {
                // Use the centralized database initialization method
                await app.InitializeDatabaseAsync();
                migrateActivity?.SetTag("db.migrate.success", true);
            }
            catch (Exception ex)
            {
                migrateActivity?.SetTag("db.migrate.success", false);
                migrateActivity?.SetTag("error", true);
                migrateActivity?.SetTag("error.message", ex.Message);
                logger?.LogError(ex, "Database initialization failed.");
                throw;
            }
        }
    }
}