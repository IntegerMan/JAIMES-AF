using MattEland.Jaimes.Agents.Services;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Moq;
using RabbitMQ.Client;
using ApiServiceProgram = MattEland.Jaimes.ApiService.Program;

namespace MattEland.Jaimes.Tests.Endpoints;

public abstract class EndpointTestBase : IAsyncLifetime
{
    protected WebApplicationFactory<ApiServiceProgram> Factory = null!;
    protected HttpClient Client = null!;

    public virtual async ValueTask InitializeAsync()
    {
        // Use a unique database name per test instance
        string dbName = $"TestDb_{Guid.NewGuid()}";
        CancellationToken ct = TestContext.Current.CancellationToken;

        Factory = new WebApplicationFactory<ApiServiceProgram>()
            .WithWebHostBuilder(builder =>
            {
                // Override settings for testing
                builder.UseSetting("SkipDatabaseInitialization", "true");
                // Provide a dummy connection string to satisfy AddJaimesRepositories validation
                // This will be replaced with InMemory in ConfigureServices
                builder.UseSetting("ConnectionStrings:postgres-db",
                    "Host=localhost;Database=test;Username=test;Password=test");
                // Provide a mock messaging connection string for RabbitMQ
                builder.UseSetting("ConnectionStrings:messaging", "amqp://guest:guest@localhost:5672/");
                // Provide default Ollama configuration for text generation and embeddings
                builder.UseSetting("TextGenerationModel:Provider", "Ollama");
                builder.UseSetting("TextGenerationModel:Endpoint", "http://localhost:11434");
                builder.UseSetting("TextGenerationModel:Name", "gemma3");
                builder.UseSetting("EmbeddingModel:Provider", "Ollama");
                builder.UseSetting("EmbeddingModel:Endpoint", "http://localhost:11434");
                builder.UseSetting("EmbeddingModel:Name", "nomic-embed-text");

                builder.ConfigureServices(services =>
                {
                    // Remove ALL DbContext-related registrations added by the application
                    // We need to remove both the service registrations AND clear any internal EF Core provider tracking
                    // The key is to remove ALL descriptors that could be related to DbContext
                    List<ServiceDescriptor> toRemove = new();
                    foreach (ServiceDescriptor descriptor in services.ToList())
                    {
                        // Remove by exact service type match
                        if (descriptor.ServiceType == typeof(DbContextOptions<JaimesDbContext>) ||
                            descriptor.ServiceType == typeof(JaimesDbContext))
                        {
                            toRemove.Add(descriptor);
                            continue;
                        }

                        // Remove generic DbContextOptions<JaimesDbContext> registrations
                        if (descriptor.ServiceType.IsGenericType &&
                            descriptor.ServiceType.GetGenericTypeDefinition() == typeof(DbContextOptions<>) &&
                            descriptor.ServiceType.GetGenericArguments().Length > 0 &&
                            descriptor.ServiceType.GetGenericArguments()[0] == typeof(JaimesDbContext))
                        {
                            toRemove.Add(descriptor);
                            continue;
                        }

                        // Remove by implementation type
                        if (descriptor.ImplementationType == typeof(JaimesDbContext)) toRemove.Add(descriptor);
                    }

                    // Remove all matching descriptors
                    foreach (ServiceDescriptor descriptor in toRemove) services.Remove(descriptor);

                    // Now manually register the DbContext with InMemory
                    // We use TryAddDbContext to avoid adding if already registered, but since we've removed
                    // all existing registrations above, this should work. However, to be safe, we'll
                    // manually construct and register the options and context.
                    services.AddSingleton<DbContextOptions<JaimesDbContext>>(sp =>
                    {
                        DbContextOptionsBuilder<JaimesDbContext> optionsBuilder = new();
                        optionsBuilder.UseInMemoryDatabase(dbName, sqlOpts => { sqlOpts.EnableNullChecks(); });
                        return optionsBuilder.Options;
                    });

                    services.AddScoped<JaimesDbContext>(sp =>
                    {
                        DbContextOptions<JaimesDbContext> options =
                            sp.GetRequiredService<DbContextOptions<JaimesDbContext>>();
                        return new JaimesDbContext(options);
                    });

                    // Replace RabbitMQ connection factory with a mock
                    ServiceDescriptor? connectionFactoryDescriptor =
                        services.FirstOrDefault(d => d.ServiceType == typeof(IConnectionFactory));
                    if (connectionFactoryDescriptor != null) services.Remove(connectionFactoryDescriptor);

                    Mock<IConnectionFactory> mockConnectionFactory = new();
                    Mock<IConnection> mockConnection = new();
                    Mock<IChannel> mockChannel = new();

                    mockConnectionFactory.Setup(f => f.CreateConnectionAsync(It.IsAny<CancellationToken>()))
                        .ReturnsAsync(mockConnection.Object);
                    mockConnection.Setup(c =>
                            c.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(mockChannel.Object);

                    services.AddSingleton(mockConnectionFactory.Object);

                    // Replace IMessagePublisher with a mock
                    ServiceDescriptor? messagePublisherDescriptor =
                        services.FirstOrDefault(d => d.ServiceType == typeof(IMessagePublisher));
                    if (messagePublisherDescriptor != null) services.Remove(messagePublisherDescriptor);

                    Mock<IMessagePublisher> mockMessagePublisher = new();
                    services.AddSingleton(mockMessagePublisher.Object);

                    // Replace IChatService with a mock
                    ServiceDescriptor? chatServiceDescriptor =
                        services.FirstOrDefault(d => d.ServiceType == typeof(IChatService));
                    if (chatServiceDescriptor != null) services.Remove(chatServiceDescriptor);

                    Mock<IChatService> mockChatService = new();
                    mockChatService.Setup(s => s.GenerateInitialMessageAsync(It.IsAny<GenerateInitialMessageRequest>(),
                            It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new InitialMessageResponse
                        {
                            Message = "Welcome to the game!",
                            ThreadJson = "{}"
                        });
                    services.AddSingleton(mockChatService.Object);

                    // Replace IChatHistoryService with a mock
                    ServiceDescriptor? chatHistoryServiceDescriptor =
                        services.FirstOrDefault(d => d.ServiceType == typeof(IChatHistoryService));
                    if (chatHistoryServiceDescriptor != null) services.Remove(chatHistoryServiceDescriptor);

                    Mock<IChatHistoryService> mockChatHistoryService = new();
                    services.AddSingleton(mockChatHistoryService.Object);

                    // Remove ALL hosted services to avoid scoped resolution issues in tests
                    // Hosted services are background workers that aren't needed for endpoint testing
                    // Remove all IHostedService registrations (including factory-based ones)
                    // Also remove RagSearchStorageService registration if it exists, to prevent conflicts
                    // Use a while loop to ensure we remove all instances, as services.Remove() only removes the first match
                    while (true)
                    {
                        ServiceDescriptor? hostedService =
                            services.FirstOrDefault(d => d.ServiceType == typeof(IHostedService));
                        if (hostedService == null) break;
                        services.Remove(hostedService);
                    }

                    // Also remove RagSearchStorageService as a concrete type if registered separately
                    // This prevents it from being registered as a hosted service
                    ServiceDescriptor? ragService = services.FirstOrDefault(d =>
                        d.ImplementationType == typeof(RagSearchStorageService) &&
                        d.ServiceType == typeof(RagSearchStorageService));
                    if (ragService != null) services.Remove(ragService);

                    // Ensure IRagSearchStorageService has a mock implementation for tests
                    ServiceDescriptor? ragInterface =
                        services.FirstOrDefault(d => d.ServiceType == typeof(IRagSearchStorageService));
                    if (ragInterface != null) services.Remove(ragInterface);
                    Mock<IRagSearchStorageService> mockRagStorage = new();
                    services.AddSingleton(mockRagStorage.Object);
                });
            });

        Client = Factory.CreateClient();

        // Seed test data after initialization
        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            JaimesDbContext context = scope.ServiceProvider.GetRequiredService<JaimesDbContext>();
            await context.Database.EnsureCreatedAsync(ct);
            await SeedTestDataAsync(context, ct);
        }
    }

    protected virtual async Task SeedTestDataAsync(JaimesDbContext context, CancellationToken cancellationToken)
    {
        // Add default test data
        context.Rulesets.Add(new Ruleset {Id = "test-ruleset", Name = "Test Ruleset"});
        context.Players.Add(new Player {Id = "test-player", RulesetId = "test-ruleset", Name = "Unspecified"});
        context.Scenarios.Add(new Scenario
        {
            Id = "test-scenario", RulesetId = "test-ruleset", Name = "Unspecified", SystemPrompt = "UPDATE ME",
            NewGameInstructions = "UPDATE ME"
        });
        await context.SaveChangesAsync(cancellationToken);
    }

    public virtual async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await Factory.DisposeAsync();
    }
}