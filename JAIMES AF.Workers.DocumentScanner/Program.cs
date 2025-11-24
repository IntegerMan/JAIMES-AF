using System.Diagnostics;
using MassTransit;
using MattEland.Jaimes.DocumentProcessing.Services;
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

// Configure MassTransit
builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        // Get RabbitMQ connection string from Aspire
        string? connectionString = builder.Configuration.GetConnectionString("messaging")
            ?? builder.Configuration["ConnectionStrings:messaging"]
            ?? builder.Configuration["ConnectionStrings__messaging"];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "RabbitMQ connection string is not configured. " +
                "Expected connection string 'messaging' from Aspire.");
        }

        // Parse connection string (format: amqp://username:password@host:port/vhost)
        Uri rabbitUri = new(connectionString);
        string host = rabbitUri.Host;
        ushort port = rabbitUri.Port > 0 ? (ushort)rabbitUri.Port : (ushort)5672;
        string? username = null;
        string? password = null;
        
        if (!string.IsNullOrEmpty(rabbitUri.UserInfo))
        {
            string[] userInfo = rabbitUri.UserInfo.Split(':');
            username = userInfo[0];
            if (userInfo.Length > 1)
            {
                password = userInfo[1];
            }
        }

        cfg.Host(host, port, "/", h =>
        {
            if (!string.IsNullOrEmpty(username))
            {
                h.Username(username);
            }
            if (!string.IsNullOrEmpty(password))
            {
                h.Password(password);
            }
        });

        // Configure endpoints (MassTransit will auto-create exchanges/queues as needed)
        cfg.ConfigureEndpoints(context);
    });
});

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




