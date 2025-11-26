using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using MattEland.Jaimes.ServiceDefinitions.Services;
using RabbitMQ.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using MattEland.Jaimes.Workers.DocumentCracker.Consumers;
using MattEland.Jaimes.Workers.DocumentCracker.Configuration;
using MattEland.Jaimes.Workers.DocumentCracker.Services;
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

// Configure message publishing and consuming using RabbitMQ.Client (LavinMQ compatible)
IConnectionFactory connectionFactory = RabbitMqConnectionFactory.CreateConnectionFactory(builder.Configuration);
builder.Services.AddSingleton(connectionFactory);
builder.Services.AddSingleton<IMessagePublisher, MessagePublisher>();

// Register consumer
builder.Services.AddSingleton<IMessageConsumer<CrackDocumentMessage>, CrackDocumentConsumer>();

// Register consumer service (background service)
builder.Services.AddHostedService<MessageConsumerService<CrackDocumentMessage>>();

// Configure OpenTelemetry ActivitySource
const string activitySourceName = "Jaimes.Workers.DocumentCracker";
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




