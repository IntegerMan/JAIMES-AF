namespace MattEland.Jaimes.ApiService.Helpers;

public static class ApplicationLifetimeExtensions
{
    /// <summary>
    /// Registers database initialization to run after the application has started.
    /// This is so OpenTelemetry TracerProviders and any ActivityListeners are active and will capture the activities produced during initialization.
    /// </summary>
    public static void ScheduleDatabaseInitialization(this WebApplication app)
    {
        app.Lifetime.ApplicationStarted.Register(() =>
        {
            // Run in background to avoid blocking the ApplicationStarted callback
            _ = Task.Run(async () =>
            {
                try
                {
                    DatabaseInitializer dbInit = app.Services.GetRequiredService<DatabaseInitializer>();
                    await dbInit.InitializeAsync(app);
                }
                catch (Exception ex)
                {
                    ILogger<Program> logger = app.Services.GetRequiredService<ILogger<Program>>();
                    logger.LogError(ex, "Database initialization failed during ApplicationStarted.");

                    throw;
                }
            });
        });
    }
}