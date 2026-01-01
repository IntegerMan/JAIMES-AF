using MattEland.Jaimes.Agents.Services;
using MattEland.Jaimes.ApiService.Agents;
using MattEland.Jaimes.ApiService.Endpoints;
using MattEland.Jaimes.ApiService.Hubs;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.ServiceLayer;
using MattEland.Jaimes.Workers.Services;
using MattEland.Jaimes.ApiService.Services;

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

        // Add AG-UI Backend
        builder.Services.AddAGUI();

        // Add Swagger services
        builder.Services.AddEndpointsApiExplorer();

        builder.Services.AddFastEndpoints().SwaggerDocument();

        // Register a shared ActivitySource instance with the same name used by OpenTelemetry
        builder.Services.AddSingleton(new ActivitySource(builder.Environment.ApplicationName ?? "Jaimes.ApiService"));

        // Configure text generation service (supports Ollama, Azure OpenAI, and OpenAI)
        // Configuration is provided by AppHost via environment variables
        builder.Services.AddChatClient(
            builder.Configuration,
            "TextGenerationModel");

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

        // Register QdrantConversationsStore
        ActivitySource qdrantConversationsActivitySource = new("Jaimes.Agents.QdrantConversations");
        builder.Services.AddSingleton(qdrantConversationsActivitySource);
        builder.Services.AddSingleton<IQdrantConversationsStore, QdrantConversationsStore>();

        // Register embedding generator for rules (supports Ollama, Azure OpenAI, and OpenAI)
        // Configuration is provided by AppHost via environment variables
        builder.Services.AddEmbeddingGenerator(
            builder.Configuration,
            "EmbeddingModel");

        // Add Jaimes repositories and services
        builder.Services.AddJaimesRepositories(builder.Configuration);
        builder.Services.AddJaimesServices();

        // CRITICAL: Remove any scoped IHostedService registrations that might have been added by service scanning
        // This must happen AFTER AddJaimesServices() in case it scans and registers something as scoped
        List<ServiceDescriptor> scopedHostedServices = builder.Services
            .Where(d => d.ServiceType == typeof(IHostedService) && d.Lifetime == ServiceLifetime.Scoped)
            .ToList();
        foreach (ServiceDescriptor descriptor in scopedHostedServices)
        {
            builder.Services.Remove(descriptor);
        }

        // CRITICAL: Ensure RagSearchStorageService is registered as singleton (remove any scoped registrations)
        // Remove ALL registrations of RagSearchStorageService (in case it was registered as scoped by scanning)
        builder.Services.RemoveAll(typeof(RagSearchStorageService));
        builder.Services.AddSingleton<RagSearchStorageService>();

        // Register as IHostedService - must be singleton for host to resolve from root provider
        builder.Services.AddSingleton<IHostedService>(provider =>
            provider.GetRequiredService<RagSearchStorageService>());

        // Register the interface to resolve to the same instance
        builder.Services.RemoveAll(typeof(IRagSearchStorageService));
        builder.Services.AddSingleton<IRagSearchStorageService>(provider =>
            provider.GetRequiredService<RagSearchStorageService>());

        // Register Agents services explicitly (not auto-registered)
        builder.Services.AddScoped<IRulesSearchService, RulesSearchService>();
        builder.Services.AddScoped<IConversationSearchService, ConversationSearchService>();
        builder.Services.AddScoped<GameConversationMemoryProviderFactory>();

        // Register tool call tracking service (scoped per request)
        builder.Services.AddScoped<IToolCallTracker, ToolCallTracker>();

        // Register HttpContextAccessor for GameAwareAgent
        builder.Services.AddHttpContextAccessor();

        // Register GameAwareAgent as singleton for use with MapAGUI
        // It's safe to be singleton because it uses IHttpContextAccessor for per-request context
        builder.Services.AddSingleton<GameAwareAgent>();

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

        // Add SignalR for real-time message updates
        builder.Services.AddSignalR();
        builder.Services.AddSingleton<IMessageUpdateNotifier, SignalRMessageUpdateNotifier>();

        WebApplication app = builder.Build();

        app.ScheduleDatabaseInitialization();

        // Configure the HTTP request pipeline.
        app.UseExceptionHandler();

        if (app.Environment.IsDevelopment()) app.MapOpenApi();

        app.MapDefaultEndpoints();
        app.UseFastEndpoints().UseSwaggerGen();

        // Map manual sentiment update endpoint
        app.MapUpdateMessageSentiment();

        // Map evaluation maintenance endpoints
        app.MapGetMissingEvaluatorsEndpoint();
        app.MapTriggerReEvaluationEndpoint();

        // Map SignalR hub for real-time message updates
        app.MapHub<MessageHub>("/hubs/messages");

        // Map AG-UI endpoint for game-specific chat
        // Use GameAwareAgent which creates game-specific agents based on route
        app.MapAGUI("/games/{gameId:guid}/chat", app.Services.GetRequiredService<GameAwareAgent>());

        await app.RunAsync();
    }
}