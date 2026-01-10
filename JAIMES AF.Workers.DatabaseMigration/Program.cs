using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.ServiceDefaults;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.ServiceLayer;
using MattEland.Jaimes.ServiceLayer.Services;
using System.Diagnostics;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Add service defaults (telemetry, health checks, service discovery)
// This includes ConfigureOpenTelemetry() AND AddOpenTelemetryExporters() for OTLP export
builder.AddServiceDefaults();


// Load configuration
builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", false, false)
    .AddJsonFile("appsettings.Development.json", true, false)
    .AddUserSecrets(typeof(Program).Assembly)
    .AddEnvironmentVariables()
    .AddCommandLine(args);

// Add PostgreSQL with EF Core
builder.Services.AddJaimesRepositories(builder.Configuration);

// Add registration services
builder.Services.AddScoped<IToolRegistrar, ToolRegistrar>();
builder.Services.AddScoped<IEvaluatorRegistrar, EvaluatorRegistrar>();
builder.Services.AddScoped<IClassificationModelService, ClassificationModelService>();

// Configure OpenTelemetry ActivitySource
const string activitySourceName = "Jaimes.Workers.DatabaseMigration";
ActivitySource activitySource = new(activitySourceName);


// Register ActivitySource for dependency injection
builder.Services.AddSingleton(activitySource);

// Build host
using IHost host = builder.Build();

ILogger<Program> logger = host.Services.GetRequiredService<ILogger<Program>>();

logger.LogInformation("Starting Database Migration Worker");

try
{
    await host.ApplyMigrationsAsync();
    logger.LogInformation("Database migrations completed successfully.");

    // Auto-register tools and evaluators
    using var scope = host.Services.CreateScope();
    var toolRegistrar = scope.ServiceProvider.GetRequiredService<IToolRegistrar>();
    var evaluatorRegistrar = scope.ServiceProvider.GetRequiredService<IEvaluatorRegistrar>();

    logger.LogInformation("Registering tools in database...");
    await toolRegistrar.RegisterToolsAsync();

    logger.LogInformation("Registering evaluators in database...");
    await evaluatorRegistrar.RegisterEvaluatorsAsync();

    // Upload classification model if not already present
    var modelService = scope.ServiceProvider.GetRequiredService<IClassificationModelService>();
    await UploadClassificationModelIfNeededAsync(modelService, logger);

    logger.LogInformation("Tool and evaluator registration completed. Exiting migration worker.");
}
catch (Exception ex)
{
    logger.LogError(ex, "Failed to apply database migrations. Migration worker will exit with error.");
    Environment.ExitCode = 1;
    throw;
}
finally
{
    // Properly stop the host to trigger OpenTelemetry flush.
    // This ensures all traces and logs are exported before the process exits.
    await host.StopAsync();
}

/// <summary>
/// Uploads the bundled sentiment classification model to the database if not already present.
/// </summary>
static async Task UploadClassificationModelIfNeededAsync(
    IClassificationModelService modelService,
    ILogger logger)
{
    // Check if model already exists in database
    var existingModel = await modelService.GetLatestModelAsync(ClassificationModelTypes.SentimentClassification);
    if (existingModel != null)
    {
        logger.LogInformation(
            "Sentiment classification model already exists in database (Id: {Id}, Created: {Created})",
            existingModel.Id,
            existingModel.CreatedAt);
        return;
    }

    // Look for bundled model file
    string modelPath = Path.Combine(AppContext.BaseDirectory, "SentimentModel.zip");
    if (!File.Exists(modelPath))
    {
        logger.LogWarning(
            "No bundled sentiment model found at {ModelPath}. Model will be trained by UserMessageWorker on first run.",
            modelPath);
        return;
    }

    // Upload the model to the database
    logger.LogInformation("Uploading bundled sentiment model to database from {ModelPath}", modelPath);
    byte[] modelContent = await File.ReadAllBytesAsync(modelPath);

    await modelService.UploadModelAsync(
        ClassificationModelTypes.SentimentClassification,
        "Sentiment Classification Model",
        "SentimentModel.zip",
        modelContent,
        "ML.NET AutoML-trained sentiment classification model for user message analysis.");

    logger.LogInformation("Sentiment classification model uploaded to database successfully.");
}

