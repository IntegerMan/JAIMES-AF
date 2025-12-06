using FastEndpoints.Swagger;
using MattEland.Jaimes.Agents.Services;
using MattEland.Jaimes.ApiService.Helpers;
using MattEland.Jaimes.ServiceDefaults;
using MattEland.Jaimes.Services;
using MattEland.Jaimes.ServiceDefinitions.Configuration;
using MattEland.Jaimes.Workers.Services;
using RabbitMQ.Client;

namespace MattEland.Jaimes.ApiService;

public class Program
{
    public static async Task Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        // Add service defaults & Aspire client integrations.
        builder.AddServiceDefaults();

        // Configure message publishing using RabbitMQ.Client (LavinMQ compatible)
        IConnectionFactory connectionFactory = RabbitMqConnectionFactory.CreateConnectionFactory(builder.Configuration);
        builder.Services.AddSingleton(connectionFactory);
        builder.Services.AddSingleton<IMessagePublisher, MessagePublisher>();

        // Add services to the container.
        builder.Services.AddProblemDetails();

        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();

        // Add Swagger services
        builder.Services.AddEndpointsApiExplorer();

        builder.Services.AddFastEndpoints().SwaggerDocument();

        // Register a shared ActivitySource instance with the same name used by OpenTelemetry
        builder.Services.AddSingleton(new ActivitySource(builder.Environment.ApplicationName ?? "Jaimes.ApiService"));

        // Configure text generation service (supports Ollama, Azure OpenAI, and OpenAI)
        // Get Ollama endpoint and model from Aspire connection strings (for default Ollama provider)
        string? ollamaConnectionString = builder.Configuration.GetConnectionString("chatModel");
        (string? ollamaEndpoint, string? ollamaModel) =
            EmbeddingServiceExtensions.ParseOllamaConnectionString(ollamaConnectionString);

        // Register text generation service
        builder.Services.AddChatClient(
            builder.Configuration,
            "TextGenerationModel",
            ollamaEndpoint,
            ollamaModel);

        // Keep JaimesChatOptions for backward compatibility (may be used by other services)
        JaimesChatOptions? chatOptions = builder.Configuration.GetSection("ChatService").Get<JaimesChatOptions>();
        if (chatOptions != null) builder.Services.AddSingleton(chatOptions);

        // Configure VectorDbOptions from configuration and register instance for DI
        VectorDbOptions vectorDbOptions = builder.Configuration.GetSection("VectorDb").Get<VectorDbOptions>() ??
                                          throw new InvalidOperationException("VectorDb configuration is required");
        builder.Services.AddSingleton(vectorDbOptions);

        // Register Qdrant-based rules search services
        // Get Qdrant client (already registered above for document embeddings)
        // Register ActivitySource for QdrantRulesStore
        ActivitySource qdrantRulesActivitySource = new("Jaimes.Agents.QdrantRules");
        builder.Services.AddSingleton(qdrantRulesActivitySource);

        // Register QdrantRulesStore
        builder.Services.AddSingleton<IQdrantRulesStore, QdrantRulesStore>();

        // Register RAG search storage service (BackgroundService with async queue)
        // Register as both the interface and as a hosted service (singleton)
        // BackgroundService implements IHostedService and must be registered as singleton
        // AddHostedService<T> automatically registers T as a singleton, so we register it first
        // Then register the interface to resolve to the same instance
        builder.Services.AddHostedService<RagSearchStorageService>();
        builder.Services.AddSingleton<IRagSearchStorageService>(provider =>
            provider.GetRequiredService<RagSearchStorageService>());

        // Register embedding generator for rules (supports Ollama, Azure OpenAI, and OpenAI)
        // Get Ollama endpoint and model from Aspire connection strings (for default Ollama provider)
        // Get the embedding model connection string provided by Aspire via .WithReference(embedModel)
        string? embedConnectionString = builder.Configuration.GetConnectionString("embedModel");
        (string? embedOllamaEndpoint, string? embedOllamaModel) =
            EmbeddingServiceExtensions.ParseOllamaConnectionString(embedConnectionString);

        builder.Services.AddEmbeddingGenerator(
            builder.Configuration,
            "EmbeddingModel",
            embedOllamaEndpoint,
            embedOllamaModel);

        // Add Jaimes repositories and services
        builder.Services.AddJaimesRepositories(builder.Configuration);
        builder.Services.AddJaimesServices();

        // Register DatabaseInitializer for DI
        builder.Services.AddSingleton<DatabaseInitializer>();

        // Configure Qdrant client for embedding management using centralized extension method
        // ApiService uses "qdrant" as default API key and allows fallback to localhost:6334
        builder.Services.AddQdrantClient(builder.Configuration,
            new QdrantExtensions.QdrantConfigurationOptions
            {
                SectionPrefix = "DocumentChunking",
                ConnectionStringName = "qdrant-embeddings",
                RequireConfiguration = false, // Allow fallback to localhost:6334
                DefaultApiKey = "qdrant", // ApiService defaults to "qdrant" API key
                AdditionalApiKeyKeys = new[]
                {
                    "DocumentChunking:QdrantApiKey",
                    "DocumentChunking__QdrantApiKey",
                    "DocumentEmbedding:QdrantApiKey",
                    "DocumentEmbedding__QdrantApiKey"
                }
            });

        // Configure DocumentChunkingOptions (which includes Qdrant configuration)
        DocumentChunkingOptions chunkingOptions =
            builder.Configuration.GetSection("DocumentChunking").Get<DocumentChunkingOptions>()
            ?? new DocumentChunkingOptions();
        builder.Services.AddSingleton(chunkingOptions);

        // Register ActivitySource for QdrantEmbeddingStore
        ActivitySource qdrantActivitySource = new("Jaimes.ApiService.Qdrant");
        builder.Services.AddSingleton(qdrantActivitySource);

        // Always register QdrantEmbeddingStore - it will handle missing configuration gracefully
        builder.Services.AddSingleton<IQdrantEmbeddingStore, QdrantEmbeddingStore>();

        WebApplication app = builder.Build();

        app.ScheduleDatabaseInitialization();

        // Configure the HTTP request pipeline.
        app.UseExceptionHandler();

        if (app.Environment.IsDevelopment()) app.MapOpenApi();

        app.MapDefaultEndpoints();
        app.UseFastEndpoints().UseSwaggerGen();

        await app.RunAsync();
    }
}