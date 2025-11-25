using System;
using System.Globalization;
using FastEndpoints;
using FastEndpoints.Swagger;
using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.ServiceDefaults;
using System.Diagnostics;
using MattEland.Jaimes.ApiService.Helpers;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.Services;
using MassTransit;
using MongoDB.Driver;
using Qdrant.Client;
using MattEland.Jaimes.Workers.DocumentEmbeddings.Services;
using MattEland.Jaimes.Workers.DocumentEmbeddings.Configuration;
using MattEland.Jaimes.Agents.Services;

namespace MattEland.Jaimes.ApiService;

public class Program
{
    public static async Task Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        // Add service defaults & Aspire client integrations.
        builder.AddServiceDefaults();

        // Add MongoDB client integration when connection information is available (Aspire/local config)
        string? mongoConnectionString = builder.Configuration.GetConnectionString("documents")
            ?? builder.Configuration["ConnectionStrings:documents"]
            ?? builder.Configuration["ConnectionStrings__documents"]
            ?? builder.Configuration["Aspire:MongoDB:Driver:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(mongoConnectionString))
        {
            builder.AddMongoDBClient("documents");
        }
        else
        {
            builder.Services.AddSingleton<IMongoClient>(_ =>
                new MongoClient("mongodb://localhost:27017"));
        }

        string? messagingConnectionString = builder.Configuration.GetConnectionString("messaging")
            ?? builder.Configuration["ConnectionStrings:messaging"]
            ?? builder.Configuration["ConnectionStrings__messaging"];
        bool rabbitMqConfigured = !string.IsNullOrWhiteSpace(messagingConnectionString);

        // Configure MassTransit for publishing messages (RabbitMQ when available, in-memory otherwise)
        builder.Services.AddMassTransit(x =>
        {
            if (rabbitMqConfigured)
            {
                x.UsingRabbitMq((context, cfg) =>
                {
                    ConfigureRabbitMq(cfg, messagingConnectionString!);
                    cfg.ConfigureEndpoints(context);
                });
            }
            else
            {
                x.UsingInMemory((context, cfg) =>
                {
                    cfg.ConfigureEndpoints(context);
                });
            }
        });

        // Add services to the container.
        builder.Services.AddProblemDetails();

        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();
        
        // Add Swagger services
        builder.Services.AddEndpointsApiExplorer();

        builder.Services.AddFastEndpoints().SwaggerDocument();

        // Register a shared ActivitySource instance with the same name used by OpenTelemetry
        builder.Services.AddSingleton(new ActivitySource(builder.Environment.ApplicationName ?? "Jaimes.ApiService"));

        // Configure JaimesChatOptions from configuration and register instance for DI
        JaimesChatOptions chatOptions = builder.Configuration.GetSection("ChatService").Get<JaimesChatOptions>() ?? throw new InvalidOperationException("ChatService configuration is required");
        builder.Services.AddSingleton(chatOptions);

        // Configure VectorDbOptions from configuration and register instance for DI
        VectorDbOptions vectorDbOptions = builder.Configuration.GetSection("VectorDb").Get<VectorDbOptions>() ?? throw new InvalidOperationException("VectorDb configuration is required");
        builder.Services.AddSingleton(vectorDbOptions);

        // Register Qdrant-based rules search services
        // Get Qdrant client (already registered above for document embeddings)
        // Register ActivitySource for QdrantRulesStore
        ActivitySource qdrantRulesActivitySource = new("Jaimes.Agents.QdrantRules");
        builder.Services.AddSingleton(qdrantRulesActivitySource);
        
        // Register QdrantRulesStore
        builder.Services.AddSingleton<IQdrantRulesStore, QdrantRulesStore>();
        
        // Register Azure OpenAI embedding service for rules
        builder.Services.AddHttpClient<IAzureOpenAIEmbeddingService, AzureOpenAIEmbeddingService>();

        // Add Jaimes repositories and services
        builder.Services.AddJaimesRepositories(builder.Configuration);
        builder.Services.AddJaimesServices();

        // Register DatabaseInitializer for DI
        builder.Services.AddSingleton<DatabaseInitializer>();

        // Configure Qdrant client for embedding management
        // Use the same comprehensive configuration lookup as the DocumentEmbeddings worker
        string? qdrantHost = builder.Configuration["EmbeddingWorker:QdrantHost"]
            ?? builder.Configuration["EmbeddingWorker__QdrantHost"]
            ?? builder.Configuration["QDRANT_EMBEDDINGS_GRPCHOST"]
            ?? builder.Configuration["QdrantEmbeddings__GrpcHost"];

        string? qdrantPortStr = builder.Configuration["EmbeddingWorker:QdrantPort"]
            ?? builder.Configuration["EmbeddingWorker__QdrantPort"]
            ?? builder.Configuration["QDRANT_EMBEDDINGS_GRPCPORT"]
            ?? builder.Configuration["QdrantEmbeddings__GrpcPort"];

        string? qdrantConnectionString = builder.Configuration.GetConnectionString("qdrant-embeddings")
            ?? builder.Configuration["ConnectionStrings:qdrant-embeddings"]
            ?? builder.Configuration["ConnectionStrings__qdrant-embeddings"];

        // Initialize API key variable
        string? qdrantApiKey = null;

        // Try to extract API key from connection string first (this is the most reliable source)
        // The connection string from WithReference(qdrant) should be resolved by Aspire and may contain the API key
        if (!string.IsNullOrWhiteSpace(qdrantConnectionString))
        {
            ApplyQdrantConnectionString(qdrantConnectionString, ref qdrantHost, ref qdrantPortStr, ref qdrantApiKey);
        }

        // Fall back to other configuration sources
        qdrantHost ??= builder.Configuration["EmbeddingWorker:QdrantHost"]
            ?? builder.Configuration["EmbeddingWorker__QdrantHost"]
            ?? builder.Configuration["QDRANT_EMBEDDINGS_GRPCHOST"]
            ?? builder.Configuration["QdrantEmbeddings__GrpcHost"]
            ?? builder.Configuration["Aspire:Resources:qdrant-embeddings:Endpoints:grpc:Host"]
            ?? Environment.GetEnvironmentVariable("QDRANT_EMBEDDINGS_GRPCHOST");

        qdrantPortStr ??= builder.Configuration["EmbeddingWorker:QdrantPort"]
            ?? builder.Configuration["EmbeddingWorker__QdrantPort"]
            ?? builder.Configuration["QDRANT_EMBEDDINGS_GRPCPORT"]
            ?? builder.Configuration["QdrantEmbeddings__GrpcPort"]
            ?? builder.Configuration["Aspire:Resources:qdrant-embeddings:Endpoints:grpc:Port"]
            ?? Environment.GetEnvironmentVariable("QDRANT_EMBEDDINGS_GRPCPORT");

        // Try multiple possible API key locations
        qdrantApiKey ??= builder.Configuration["Qdrant__ApiKey"]
            ?? builder.Configuration["Qdrant:ApiKey"]
            ?? builder.Configuration["QDRANT_EMBEDDINGS_APIKEY"]
            ?? builder.Configuration["QdrantEmbeddings__ApiKey"]
            ?? builder.Configuration["QDRANT_EMBEDDINGS_API_KEY"]
            ?? builder.Configuration["EmbeddingWorker:QdrantApiKey"]
            ?? builder.Configuration["Aspire:Resources:qdrant-embeddings:ApiKey"]
            ?? Environment.GetEnvironmentVariable("Qdrant__ApiKey")
            ?? Environment.GetEnvironmentVariable("QDRANT_EMBEDDINGS_APIKEY")
            ?? Environment.GetEnvironmentVariable("QdrantEmbeddings__ApiKey")
            ?? Environment.GetEnvironmentVariable("QDRANT_EMBEDDINGS_API_KEY")
            ?? Environment.GetEnvironmentVariable("qdrant-api-key");

        // If the API key looks like an unresolved Aspire expression, try to resolve it
        if (!string.IsNullOrWhiteSpace(qdrantApiKey) && qdrantApiKey.Contains('{') && qdrantApiKey.Contains('}'))
        {
            string? resolvedApiKey = Environment.GetEnvironmentVariable("qdrant-api-key")
                ?? Environment.GetEnvironmentVariable("QDRANT_API_KEY")
                ?? Environment.GetEnvironmentVariable("Qdrant__ApiKey")
                ?? Environment.GetEnvironmentVariable("QDRANT_EMBEDDINGS_APIKEY");

            if (!string.IsNullOrWhiteSpace(resolvedApiKey) && !resolvedApiKey.Contains('{'))
            {
                qdrantApiKey = resolvedApiKey;
            }
            else
            {
                qdrantApiKey = "qdrant";
            }
        }

        // If API key is still not found, use the default value
        if (string.IsNullOrWhiteSpace(qdrantApiKey))
        {
            qdrantApiKey = "qdrant";
        }

        // Always register QdrantEmbeddingStore - configuration is handled inside the service
        // Use default values if configuration is not found (for local development)
        int qdrantPort = 6334; // Default Qdrant gRPC port
        if (!string.IsNullOrWhiteSpace(qdrantPortStr) && int.TryParse(qdrantPortStr, out int parsedPort))
        {
            qdrantPort = parsedPort;
        }
        else if (string.IsNullOrWhiteSpace(qdrantHost))
        {
            // If no host is configured, use localhost as fallback
            qdrantHost = "localhost";
        }

        bool useHttps = builder.Configuration.GetValue<bool>("EmbeddingWorker:QdrantUseHttps", defaultValue: false);
        
        // Always register QdrantClient - always pass API key if we have one (even if it's "qdrant")
        // Qdrant requires authentication, so we must pass the API key
        // Ensure qdrantHost is never null (fallback to localhost if not configured)
        string qdrantHostFinal = qdrantHost ?? "localhost";
        QdrantClient qdrantClient = string.IsNullOrWhiteSpace(qdrantApiKey)
            ? new QdrantClient(qdrantHostFinal, port: qdrantPort, https: useHttps)
            : new QdrantClient(qdrantHostFinal, port: qdrantPort, https: useHttps, apiKey: qdrantApiKey);
        builder.Services.AddSingleton(qdrantClient);

        // Configure EmbeddingWorkerOptions
        EmbeddingWorkerOptions embeddingOptions = builder.Configuration.GetSection("EmbeddingWorker").Get<EmbeddingWorkerOptions>()
            ?? new EmbeddingWorkerOptions();
        builder.Services.AddSingleton(embeddingOptions);

        // Register ActivitySource for QdrantEmbeddingStore
        ActivitySource qdrantActivitySource = new("Jaimes.ApiService.Qdrant");
        builder.Services.AddSingleton(qdrantActivitySource);

        // Always register QdrantEmbeddingStore - it will handle missing configuration gracefully
        builder.Services.AddSingleton<IQdrantEmbeddingStore, QdrantEmbeddingStore>();

        WebApplication app = builder.Build();

        app.ScheduleDatabaseInitialization();

        // Configure the HTTP request pipeline.
        app.UseExceptionHandler();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.MapDefaultEndpoints();
        app.UseFastEndpoints().UseSwaggerGen();

        await app.RunAsync();
    }

    private static void ConfigureRabbitMq(IRabbitMqBusFactoryConfigurator cfg, string connectionString)
    {
        if (cfg == null)
        {
            throw new ArgumentNullException(nameof(cfg));
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string is required.", nameof(connectionString));
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
    }

    private static void ApplyQdrantConnectionString(
        string connectionString,
        ref string? host,
        ref string? port,
        ref string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        if (Uri.TryCreate(connectionString, UriKind.Absolute, out Uri? uri))
        {
            host ??= uri.Host;
            if (uri.Port > 0)
            {
                port ??= uri.Port.ToString(CultureInfo.InvariantCulture);
            }

            ExtractApiKeyFromQuery(uri.Query, ref apiKey);
            return;
        }

        if (TryParseHostAndPort(connectionString, out string? parsedHost, out string? parsedPort))
        {
            host ??= parsedHost;
            if (string.IsNullOrWhiteSpace(port) && !string.IsNullOrWhiteSpace(parsedPort))
            {
                port = parsedPort;
            }
        }

        string[] segments = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (string segment in segments)
        {
            string[] keyValue = segment.Split('=', 2, StringSplitOptions.TrimEntries);
            if (keyValue.Length != 2)
            {
                continue;
            }

            string key = keyValue[0];
            string value = keyValue[1];

            if (string.Equals(key, "Endpoint", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "Uri", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "GrpcUri", StringComparison.OrdinalIgnoreCase))
            {
                ApplyQdrantConnectionString(value, ref host, ref port, ref apiKey);
                continue;
            }

            if (string.Equals(key, "Host", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "Hostname", StringComparison.OrdinalIgnoreCase))
            {
                host ??= value;
                continue;
            }

            if (string.Equals(key, "Port", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "GrpcPort", StringComparison.OrdinalIgnoreCase))
            {
                port ??= value;
                continue;
            }

            if (string.Equals(key, "ApiKey", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "Api-Key", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "Api_Key", StringComparison.OrdinalIgnoreCase))
            {
                apiKey ??= value;
            }
        }
    }

    private static bool TryParseHostAndPort(string value, out string? host, out string? port)
    {
        host = null;
        port = null;

        if (string.IsNullOrWhiteSpace(value) || value.Contains('='))
        {
            return false;
        }

        string[] hostParts = value.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (hostParts.Length == 0)
        {
            return false;
        }

        host = hostParts[0];
        if (hostParts.Length > 1)
        {
            port = hostParts[1];
        }

        return true;
    }

    private static void ExtractApiKeyFromQuery(string query, ref string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(query) || !string.IsNullOrWhiteSpace(apiKey))
        {
            return;
        }

        string trimmedQuery = query.TrimStart('?');
        string[] pairs = trimmedQuery.Split('&', StringSplitOptions.RemoveEmptyEntries);

        foreach (string pair in pairs)
        {
            string[] keyValue = pair.Split('=', 2);
            if (keyValue.Length != 2)
            {
                continue;
            }

            string key = keyValue[0];
            if (string.Equals(key, "api-key", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "apikey", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "api_key", StringComparison.OrdinalIgnoreCase))
            {
                apiKey = Uri.UnescapeDataString(keyValue[1]);
                break;
            }
        }
    }
}