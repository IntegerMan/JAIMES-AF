using MattEland.Jaimes.ServiceDefinitions.Configuration;
using MattEland.Jaimes.ServiceLayer;

namespace MattEland.Jaimes.Tests.ServiceDefaults;

/// <summary>
/// Tests to verify that the API service can be constructed and all dependency injection registrations succeed.
/// This test specifically validates that IHostedService implementations are registered as singletons.
/// </summary>
public class ApiServiceDependencyInjectionTests
{
    [Fact]
    public void ConfigureServices_ShouldRegisterAllServicesSuccessfully()
    {
        // Arrange
        ServiceCollection services = new();
        IConfiguration configuration = CreateTestConfiguration();

        // Act - Configure services exactly as Program.cs does
        ConfigureServices(services, configuration);

        // Assert - Build service provider and verify no errors
        ServiceProvider serviceProvider = services.BuildServiceProvider();
        serviceProvider.ShouldNotBeNull();
    }

    [Fact]
    public void ConfigureServices_ShouldRegisterHostedServicesAsSingletons()
    {
        // Arrange
        ServiceCollection services = new();
        IConfiguration configuration = CreateTestConfiguration();

        // Act
        ConfigureServices(services, configuration);

        // Assert - Verify IHostedService registrations are singletons
        IEnumerable<ServiceDescriptor> hostedServiceDescriptors = services
            .Where(d => d.ServiceType == typeof(IHostedService));

        hostedServiceDescriptors.ShouldNotBeEmpty("At least one IHostedService should be registered");

        foreach (ServiceDescriptor descriptor in hostedServiceDescriptors)
        {
            string serviceName = descriptor.ImplementationType?.Name
                ?? descriptor.ImplementationInstance?.GetType().Name
                ?? (descriptor.ImplementationFactory != null ? $"factory-based (returns {GetFactoryReturnType(descriptor.ImplementationFactory)})" : "unknown");

            // Get more details for debugging
            string details = $"ServiceType: {descriptor.ServiceType.Name}, " +
                           $"ImplementationType: {descriptor.ImplementationType?.Name ?? "null"}, " +
                           $"Lifetime: {descriptor.Lifetime}";

            descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton,
                $"IHostedService implementation '{serviceName}' must be registered as Singleton, but was {descriptor.Lifetime}. Details: {details}");
        }
    }

    private static string GetFactoryReturnType(Func<IServiceProvider, object> factory)
    {
        try
        {
            // Try to invoke the factory with a mock provider to see what it returns
            // But we can't do that safely, so just return the factory method info if available
            return factory.Method.ReturnType.Name;
        }
        catch
        {
            return "unknown type";
        }
    }

    [Fact]
    public void ConfigureServices_ShouldResolveHostedServicesFromRootProvider()
    {
        // Arrange
        ServiceCollection services = new();
        IConfiguration configuration = CreateTestConfiguration();

        // Act
        ConfigureServices(services, configuration);
        ServiceProvider serviceProvider = services.BuildServiceProvider();

        // Assert - Should be able to resolve IEnumerable<IHostedService> from root provider
        // This is what the host does during startup
        IEnumerable<IHostedService> hostedServices = serviceProvider.GetRequiredService<IEnumerable<IHostedService>>();
        hostedServices.ShouldNotBeNull();
    }

    [Fact]
    public void ConfigureServices_ShouldResolveRagSearchStorageService()
    {
        // Arrange
        ServiceCollection services = new();
        IConfiguration configuration = CreateTestConfiguration();

        // Act
        ConfigureServices(services, configuration);
        ServiceProvider serviceProvider = services.BuildServiceProvider();

        // Assert - Should be able to resolve RagSearchStorageService
        RagSearchStorageService service = serviceProvider.GetRequiredService<RagSearchStorageService>();
        service.ShouldNotBeNull();

        // Should also be able to resolve as IHostedService
        IHostedService hostedService = serviceProvider.GetRequiredService<IHostedService>();
        hostedService.ShouldNotBeNull();
        hostedService.ShouldBeOfType<RagSearchStorageService>();

        // Should also be able to resolve as IRagSearchStorageService
        IRagSearchStorageService interfaceService = serviceProvider.GetRequiredService<IRagSearchStorageService>();
        interfaceService.ShouldNotBeNull();
        interfaceService.ShouldBeOfType<RagSearchStorageService>();

        // All should be the same instance (singleton)
        ReferenceEquals(service, hostedService).ShouldBeTrue("RagSearchStorageService should be the same instance as IHostedService");
        ReferenceEquals(service, interfaceService).ShouldBeTrue("RagSearchStorageService should be the same instance as IRagSearchStorageService");
    }

    [Fact]
    public void ConfigureServices_ShouldResolveAllRequiredServices()
    {
        // Arrange
        ServiceCollection services = new();
        IConfiguration configuration = CreateTestConfiguration();

        // Act
        ConfigureServices(services, configuration);
        ServiceProvider serviceProvider = services.BuildServiceProvider();

        // Assert - Verify key services can be resolved (skip RabbitMQ-dependent services in tests)
        // IConnectionFactory and IMessagePublisher require a running RabbitMQ broker, so skip those
        serviceProvider.GetRequiredService<IQdrantRulesStore>().ShouldNotBeNull();
        serviceProvider.GetRequiredService<IRulesSearchService>().ShouldNotBeNull();
        serviceProvider.GetRequiredService<DatabaseInitializer>().ShouldNotBeNull();
    }

    private static IConfiguration CreateTestConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Connection strings
                {"ConnectionStrings:postgres-db", "Host=localhost;Database=test;Username=test;Password=test"},
                {"ConnectionStrings:messaging", "amqp://guest:guest@localhost:5672/"},
                {"ConnectionStrings:chatModel", "http://localhost:11434|gemma3"},
                {"ConnectionStrings:embedModel", "http://localhost:11434|nomic-embed-text"},
                {"ConnectionStrings:qdrant-embeddings", "http://localhost:6334"},

                // Text generation model
                {"TextGenerationModel:Provider", "Ollama"},
                {"TextGenerationModel:Endpoint", "http://localhost:11434"},
                {"TextGenerationModel:Name", "gemma3"},

                // Embedding model
                {"EmbeddingModel:Provider", "Ollama"},
                {"EmbeddingModel:Endpoint", "http://localhost:11434"},
                {"EmbeddingModel:Name", "nomic-embed-text"},

                // Vector DB
                {"VectorDb:Provider", "Qdrant"},
                {"VectorDb:Endpoint", "http://localhost:6334"},

                // Document chunking
                {"DocumentChunking:QdrantEndpoint", "http://localhost:6334"},
                {"DocumentChunking:CollectionName", "test-collection"}
            })
            .Build();
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Add logging (required for many services)
        services.AddLogging();
        services.AddHttpClient();

        // Add minimal service defaults (without full Aspire setup for testing)
        // This focuses on testing the core DI registrations

        // Configure message publishing using RabbitMQ.Client (LavinMQ compatible)
        IConnectionFactory connectionFactory = RabbitMqConnectionFactory.CreateConnectionFactory(configuration);
        services.AddSingleton(connectionFactory);
        services.AddSingleton<IMessagePublisher, MessagePublisher>();

        // Add services to the container (skip OpenAPI/FastEndpoints for test simplicity)
        services.AddProblemDetails();
        // Skip OpenAPI/FastEndpoints in tests to avoid interceptors issues
        // services.AddOpenApi();
        // services.AddEndpointsApiExplorer();
        // services.AddFastEndpoints().SwaggerDocument();

        // Register a shared ActivitySource instance
        services.AddSingleton(new ActivitySource("Jaimes.ApiService"));

        // Configure text generation service
        string? ollamaConnectionString = configuration.GetConnectionString("chatModel");
        (string? ollamaEndpoint, string? ollamaModel) =
            EmbeddingServiceExtensions.ParseOllamaConnectionString(ollamaConnectionString);

        services.AddChatClient(
            configuration,
            "TextGenerationModel",
            ollamaEndpoint,
            ollamaModel);

        // Keep JaimesChatOptions for backward compatibility
        JaimesChatOptions? chatOptions = configuration.GetSection("ChatService").Get<JaimesChatOptions>();
        if (chatOptions != null) services.AddSingleton(chatOptions);

        // Configure VectorDbOptions
        VectorDbOptions vectorDbOptions = configuration.GetSection("VectorDb").Get<VectorDbOptions>() ??
                                          throw new InvalidOperationException("VectorDb configuration is required");
        services.AddSingleton(vectorDbOptions);

        // Register Qdrant-based rules search services
        ActivitySource qdrantRulesActivitySource = new("Jaimes.Agents.QdrantRules");
        services.AddSingleton(qdrantRulesActivitySource);

        // Register QdrantRulesStore
        services.AddSingleton<IQdrantRulesStore, QdrantRulesStore>();

        // Register RAG search storage service (BackgroundService with async queue)
        // BackgroundService implements IHostedService and must be registered as singleton
        // Register as singleton first
        services.AddSingleton<RagSearchStorageService>();
        // Register as IHostedService - must be singleton for host to resolve from root provider
        services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<RagSearchStorageService>());
        // Register the interface to resolve to the same instance
        services.AddSingleton<IRagSearchStorageService>(provider =>
            provider.GetRequiredService<RagSearchStorageService>());

        // Register embedding generator for rules
        string? embedConnectionString = configuration.GetConnectionString("embedModel");
        (string? embedOllamaEndpoint, string? embedOllamaModel) =
            EmbeddingServiceExtensions.ParseOllamaConnectionString(embedConnectionString);

        services.AddEmbeddingGenerator(
            configuration,
            "EmbeddingModel",
            embedOllamaEndpoint,
            embedOllamaModel);

        // Add Jaimes repositories and services
        services.AddJaimesRepositories(configuration);
        services.AddJaimesServices();

        // CRITICAL: Remove any scoped IHostedService registrations that might have been added by service scanning
        // This must happen AFTER AddJaimesServices() in case it scans and registers something as scoped
        List<ServiceDescriptor> scopedHostedServices = services
            .Where(d => d.ServiceType == typeof(IHostedService) && d.Lifetime == ServiceLifetime.Scoped)
            .ToList();
        foreach (ServiceDescriptor descriptor in scopedHostedServices)
        {
            services.Remove(descriptor);
        }

        // Register Agents services explicitly (not auto-registered)
        services.AddScoped<IRulesSearchService, RulesSearchService>();

        // Register DatabaseInitializer for DI
        services.AddSingleton<DatabaseInitializer>();

        // Configure Qdrant client for embedding management
        services.AddQdrantClient(configuration,
            new QdrantExtensions.QdrantConfigurationOptions
            {
                SectionPrefix = "DocumentChunking",
                ConnectionStringName = "qdrant-embeddings",
                RequireConfiguration = false,
                DefaultApiKey = "qdrant",
                AdditionalApiKeyKeys = new[]
                {
                    "DocumentChunking:QdrantApiKey",
                    "DocumentChunking__QdrantApiKey",
                    "DocumentEmbedding:QdrantApiKey",
                    "DocumentEmbedding__QdrantApiKey"
                }
            });

        // Configure DocumentChunkingOptions
        DocumentChunkingOptions chunkingOptions =
            configuration.GetSection("DocumentChunking").Get<DocumentChunkingOptions>()
            ?? new DocumentChunkingOptions();
        services.AddSingleton(chunkingOptions);

        // Register ActivitySource for QdrantEmbeddingStore
        ActivitySource qdrantActivitySource = new("Jaimes.ApiService.Qdrant");
        services.AddSingleton(qdrantActivitySource);

        // Always register QdrantEmbeddingStore
        services.AddSingleton<IQdrantEmbeddingStore, QdrantEmbeddingStore>();
    }
}

