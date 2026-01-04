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
    .AddJsonFile("appsettings.json", false, false)
    .AddJsonFile("appsettings.Development.json", true, false)
    .AddUserSecrets(typeof(Program).Assembly)
    .AddEnvironmentVariables()
    .AddCommandLine(args);

// Bind configuration using IOptions pattern
builder.Services.Configure<DocumentCrackerWorkerOptions>(
    builder.Configuration.GetSection("DocumentCrackerWorker"));

// Also keep a direct instance for backward compatibility with existing code
DocumentCrackerWorkerOptions options =
    builder.Configuration.GetSection("DocumentCrackerWorker").Get<DocumentCrackerWorkerOptions>()
    ?? throw new InvalidOperationException("DocumentCrackerWorker configuration section is required");

builder.Services.AddSingleton(options);

// Configure DocumentCrackingOptions for the cracking service
// This reads from DocumentCrackerWorker config and maps UploadDocumentsWhenCracking
builder.Services.Configure<MattEland.Jaimes.ServiceDefinitions.Configuration.DocumentCrackingOptions>(opts =>
{
    opts.UploadDocumentsWhenCracking = options.UploadDocumentsWhenCracking;
});

// Add PostgreSQL with EF Core
builder.Services.AddJaimesRepositories(builder.Configuration);

// Register shared worker services
builder.Services.AddSingleton<IPdfTextExtractor, MattEland.Jaimes.Workers.Services.PdfPigTextExtractor>();
builder.Services.AddSingleton<IDocumentCrackingService, MattEland.Jaimes.Workers.Services.DocumentCrackingService>();

// Configure message publishing and consuming using RabbitMQ.Client (LavinMQ compatible)
IConnectionFactory connectionFactory = RabbitMqConnectionFactory.CreateConnectionFactory(builder.Configuration);
builder.Services.AddSingleton(connectionFactory);
builder.Services.AddSingleton<IMessagePublisher, MessagePublisher>();

// Register consumer
builder.Services.AddSingleton<IMessageConsumer<CrackDocumentMessage>, CrackDocumentConsumer>();

// Configure HTTP client for API service communication (pipeline status reporting)
string? apiBaseUrl = builder.Configuration["Services:apiservice:http:0"]
                     ?? builder.Configuration["Services:apiservice:https:0"]
                     ?? builder.Configuration["ApiService:BaseUrl"];

// Register pipeline status reporter if API service is configured
if (!string.IsNullOrEmpty(apiBaseUrl))
{
    builder.Services.AddHttpClient("ApiService", client =>
    {
        client.BaseAddress = new Uri(apiBaseUrl);
        client.Timeout = TimeSpan.FromSeconds(5);
    });

    builder.Services.AddSingleton<IPipelineStatusReporter>(sp =>
    {
        IHttpClientFactory httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
        HttpClient httpClient = httpClientFactory.CreateClient("ApiService");
        ILogger<HttpPipelineStatusReporter> reporterLogger =
            sp.GetRequiredService<ILogger<HttpPipelineStatusReporter>>();
        return new HttpPipelineStatusReporter(httpClient, reporterLogger, "DocumentCrackerWorker");
    });
}

// Register consumer service (background service) with pipeline status reporting
builder.Services.AddHostedService(sp =>
{
    IConnectionFactory connFactory = sp.GetRequiredService<IConnectionFactory>();
    IMessageConsumer<CrackDocumentMessage> consumer = sp.GetRequiredService<IMessageConsumer<CrackDocumentMessage>>();
    ILogger<MessageConsumerService<CrackDocumentMessage>> consumerLogger =
        sp.GetRequiredService<ILogger<MessageConsumerService<CrackDocumentMessage>>>();
    ActivitySource? activity = sp.GetService<ActivitySource>();
    IPipelineStatusReporter? statusReporter = sp.GetService<IPipelineStatusReporter>();

    return new MessageConsumerService<CrackDocumentMessage>(
        connFactory, consumer, consumerLogger, activity, statusReporter, "cracking");
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

await host.WaitForMigrationsAsync();

logger.LogInformation("Starting Document Cracker Worker");

await host.RunAsync();