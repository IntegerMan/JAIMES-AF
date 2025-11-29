using System;
using FastEndpoints;
using FastEndpoints.Swagger;
using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.ServiceDefaults;
using System.Diagnostics;
using MattEland.Jaimes.ApiService.Helpers;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.Services;
using RabbitMQ.Client;
using MongoDB.Driver;
using Qdrant.Client;
using MattEland.Jaimes.Workers.DocumentChunking.Services;
using MattEland.Jaimes.Workers.DocumentChunking.Configuration;
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
        string? ollamaEndpoint = builder.Configuration.GetConnectionString("gemma3")
            ?? builder.Configuration.GetConnectionString("ollama-models");
        string? ollamaModel = "gemma3"; // Default model name from Aspire

        // Parse connection string if endpoint not explicitly set
        if (!string.IsNullOrWhiteSpace(ollamaEndpoint))
        {
            if (ollamaEndpoint.Contains("Endpoint=", StringComparison.OrdinalIgnoreCase))
            {
                string[] parts = ollamaEndpoint.Split(';');
                ollamaEndpoint = parts.FirstOrDefault(p => p.StartsWith("Endpoint=", StringComparison.OrdinalIgnoreCase))
                    ?.Substring("Endpoint=".Length)
                    ?.TrimEnd('/');
            }
            else
            {
                ollamaEndpoint = ollamaEndpoint.TrimEnd('/');
            }
        }

        // Register text generation service
        builder.Services.AddChatClient(
            builder.Configuration,
            sectionName: "TextGenerationModel",
            defaultOllamaEndpoint: ollamaEndpoint,
            defaultOllamaModel: ollamaModel);

        // Keep JaimesChatOptions for backward compatibility (may be used by other services)
        JaimesChatOptions? chatOptions = builder.Configuration.GetSection("ChatService").Get<JaimesChatOptions>();
        if (chatOptions != null)
        {
            builder.Services.AddSingleton(chatOptions);
        }

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

        // Configure Qdrant client for embedding management using centralized extension method
        // ApiService uses "qdrant" as default API key and allows fallback to localhost:6334
        builder.Services.AddQdrantClient(builder.Configuration, new QdrantExtensions.QdrantConfigurationOptions
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
        DocumentChunkingOptions chunkingOptions = builder.Configuration.GetSection("DocumentChunking").Get<DocumentChunkingOptions>()
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

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.MapDefaultEndpoints();
        app.UseFastEndpoints().UseSwaggerGen();

        await app.RunAsync();
    }


}