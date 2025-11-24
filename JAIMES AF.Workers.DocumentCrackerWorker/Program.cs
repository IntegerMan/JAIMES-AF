using System.Diagnostics;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using MattEland.Jaimes.Workers.DocumentCrackerWorker.Consumers;
using MattEland.Jaimes.Workers.DocumentCrackerWorker.Configuration;
using MattEland.Jaimes.Workers.DocumentCrackerWorker.Services;
using MattEland.Jaimes.ServiceDefaults;

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
DocumentCrackerWorkerOptions options = builder.Configuration.GetSection("DocumentCrackerWorker").Get<DocumentCrackerWorkerOptions>()
    ?? throw new InvalidOperationException("DocumentCrackerWorker configuration section is required");

builder.Services.AddSingleton(options);

// Add MongoDB client integration
builder.AddMongoDBClient("documents");

// Register services
builder.Services.AddSingleton<IDocumentCrackingService, DocumentCrackingService>();

// Configure MassTransit
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<CrackDocumentConsumer>();

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

        // Configure retry policy
        cfg.UseMessageRetry(r => r.Exponential(
            retryLimit: 5,
            minInterval: TimeSpan.FromSeconds(1),
            maxInterval: TimeSpan.FromSeconds(30),
            intervalDelta: TimeSpan.FromSeconds(2)));

        // Configure consumer endpoint
        // MassTransit will automatically create the queue and bind to the appropriate exchange
        // based on the message type (CrackDocumentMessage)
        cfg.ConfigureEndpoints(context);
    });
});

// Configure OpenTelemetry ActivitySource
const string activitySourceName = "Jaimes.Workers.DocumentCrackerWorker";
ActivitySource activitySource = new(activitySourceName);

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(activitySourceName))
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

logger.LogInformation("Starting Document Cracker Worker");

await host.RunAsync();

