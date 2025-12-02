using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
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
                // Provide a mock messaging connection string for RabbitMQ
                builder.UseSetting("ConnectionStrings:messaging", "amqp://guest:guest@localhost:5672/");
                // Provide default Ollama configuration for text generation and embeddings
                builder.UseSetting("TextGenerationModel:Provider", "Ollama");
                builder.UseSetting("TextGenerationModel:Endpoint", "http://localhost:11434");
                builder.UseSetting("TextGenerationModel:Name", "gemma3");
                builder.UseSetting("EmbeddingModel:Provider", "Ollama");
                builder.UseSetting("EmbeddingModel:Endpoint", "http://localhost:11434");
                builder.UseSetting("EmbeddingModel:Name", "nomic-embed-text");
                
                // Configure test services
                builder.ConfigureServices(services =>
                {
                    // Replace database context with in-memory database
                    // Remove all DbContext-related registrations to avoid provider conflicts
                    ServiceDescriptor[] dbContextDescriptors = services
                        .Where(d => d.ServiceType == typeof(DbContextOptions<JaimesDbContext>) ||
                                   d.ServiceType == typeof(DbContextOptions) ||
                                   d.ServiceType == typeof(JaimesDbContext))
                        .ToArray();
                    
                    foreach (ServiceDescriptor descriptor in dbContextDescriptors)
                    {
                        services.Remove(descriptor);
                    }
                    
                    services.AddJaimesRepositoriesInMemory(dbName);
                    
                    // Replace RabbitMQ connection factory with a mock
                    ServiceDescriptor? connectionFactoryDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IConnectionFactory));
                    if (connectionFactoryDescriptor != null)
                    {
                        services.Remove(connectionFactoryDescriptor);
                    }
                    
                    Mock<IConnectionFactory> mockConnectionFactory = new();
                    Mock<IConnection> mockConnection = new();
                    Mock<IChannel> mockChannel = new();
                    
                    mockConnectionFactory.Setup(f => f.CreateConnectionAsync(It.IsAny<CancellationToken>()))
                        .ReturnsAsync(mockConnection.Object);
                    mockConnection.Setup(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(mockChannel.Object);
                    
                    services.AddSingleton(mockConnectionFactory.Object);
                    
                    // Replace IMessagePublisher with a mock
                    ServiceDescriptor? messagePublisherDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IMessagePublisher));
                    if (messagePublisherDescriptor != null)
                    {
                        services.Remove(messagePublisherDescriptor);
                    }
                    
                    Mock<IMessagePublisher> mockMessagePublisher = new();
                    services.AddSingleton(mockMessagePublisher.Object);
                    
                    // Replace IChatService with a mock
                    ServiceDescriptor? chatServiceDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IChatService));
                    if (chatServiceDescriptor != null)
                    {
                        services.Remove(chatServiceDescriptor);
                    }
                    
                    Mock<IChatService> mockChatService = new();
                    mockChatService.Setup(s => s.GenerateInitialMessageAsync(It.IsAny<GenerateInitialMessageRequest>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new InitialMessageResponse
                        {
                            Message = "Welcome to the game!",
                            ThreadJson = "{}"
                        });
                    services.AddSingleton(mockChatService.Object);
                    
                    // Replace IChatHistoryService with a mock
                    ServiceDescriptor? chatHistoryServiceDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IChatHistoryService));
                    if (chatHistoryServiceDescriptor != null)
                    {
                        services.Remove(chatHistoryServiceDescriptor);
                    }
                    
                    Mock<IChatHistoryService> mockChatHistoryService = new();
                    services.AddSingleton(mockChatHistoryService.Object);
                });
            });

        Client = Factory.CreateClient();

        // Seed test data after initialization
        using (var scope = Factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<JaimesDbContext>();
            await context.Database.EnsureCreatedAsync(ct);
            await SeedTestDataAsync(context, ct);
        }
    }

    protected virtual async Task SeedTestDataAsync(JaimesDbContext context, CancellationToken cancellationToken)
    {
        // Add default test data
        context.Rulesets.Add(new Ruleset { Id = "test-ruleset", Name = "Test Ruleset" });
        context.Players.Add(new Player { Id = "test-player", RulesetId = "test-ruleset", Name = "Unspecified" });
        context.Scenarios.Add(new Scenario { Id = "test-scenario", RulesetId = "test-ruleset", Name = "Unspecified", SystemPrompt = "UPDATE ME", NewGameInstructions = "UPDATE ME" });
        await context.SaveChangesAsync(cancellationToken);
    }

    public virtual async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await Factory.DisposeAsync();
    }
}
