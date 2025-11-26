using System.Diagnostics;
using MattEland.Jaimes.DocumentProcessing.Services;
using MattEland.Jaimes.ServiceDefinitions.Services;
using RabbitMQ.Client;
using MattEland.Jaimes.ServiceDefaults;
using MattEland.Jaimes.Workers.DocumentChangeDetector.Configuration;
using MattEland.Jaimes.Workers.DocumentChangeDetector.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Configure OpenTelemetry for Aspire telemetry
builder.ConfigureOpenTelemetry();

// Configure logging with OpenTelemetry
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
});

// Load configuration
builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false)
    .AddUserSecrets(typeof(Program).Assembly)
    .AddEnvironmentVariables()
    .AddCommandLine(args);

// Bind configuration
DocumentChangeDetectorOptions options = builder.Configuration.GetSection("DocumentChangeDetector").Get<DocumentChangeDetectorOptions>()
    ?? throw new InvalidOperationException("DocumentChangeDetector configuration section is required");

if (string.IsNullOrWhiteSpace(options.ContentDirectory))
{
    throw new InvalidOperationException("DocumentChangeDetector:ContentDirectory configuration is required");
}

builder.Services.AddSingleton(options);

// Add MongoDB client integration
builder.AddMongoDBClient("documents");

// Register document processing services
builder.Services.AddSingleton<IDirectoryScanner, DirectoryScanner>();
builder.Services.AddSingleton<IChangeTracker, ChangeTracker>();

// Register scanner services
builder.Services.AddSingleton<IDocumentChangeDetectorService, DocumentChangeDetectorService>();
builder.Services.AddHostedService<DocumentChangeDetectorBackgroundService>();

// Configure message publishing using RabbitMQ.Client (LavinMQ compatible)
IConnectionFactory connectionFactory = RabbitMqConnectionFactory.CreateConnectionFactory(builder.Configuration);
builder.Services.AddSingleton(connectionFactory);
builder.Services.AddSingleton<IMessagePublisher, MessagePublisher>();

// Configure OpenTelemetry ActivitySource
const string activitySourceName = "Jaimes.Workers.DocumentChangeDetector";
ActivitySource activitySource = new(activitySourceName);

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName: activitySourceName, serviceVersion: "1.0.0"))
    .WithMetrics(metrics =>
    {
        metrics.AddRuntimeInstrumentation()
            .AddHttpClientInstrumentation()
            .AddMeter(activitySourceName);
    })
    .WithTracing(tracing =>
    {
        tracing.AddSource(activitySourceName)
            .AddHttpClientInstrumentation();
    });

// Register ActivitySource for dependency injection
builder.Services.AddSingleton(activitySource);

// Build host
using IHost host = builder.Build();

ILogger<Program> logger = host.Services.GetRequiredService<ILogger<Program>>();

logger.LogInformation("Starting Document Change Detector Worker");

await host.RunAsync();

// Log and use the exit code set by the background service
int exitCode = Environment.ExitCode;
logger.LogInformation("Application exiting with code: {ExitCode}", exitCode);

// Explicitly exit with the code (though Environment.ExitCode would be used automatically)
Environment.Exit(exitCode);




