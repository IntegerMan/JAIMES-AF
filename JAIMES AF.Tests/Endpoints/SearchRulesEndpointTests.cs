using System.Net.Http.Json;
using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Moq;
using RabbitMQ.Client;
using Shouldly;
using ApiServiceProgram = MattEland.Jaimes.ApiService.Program;

namespace MattEland.Jaimes.Tests.Endpoints;

public class SearchRulesEndpointTests : EndpointTestBase
{
    private Mock<IRulesSearchService> _mockRulesSearchService = null!;

    public override async ValueTask InitializeAsync()
    {
        // Create mock before calling base
        _mockRulesSearchService = new Mock<IRulesSearchService>();
        
        // Use a unique database name per test instance
        string dbName = $"TestDb_{Guid.NewGuid()}";
        CancellationToken ct = TestContext.Current.CancellationToken;
        
        Factory = new WebApplicationFactory<ApiServiceProgram>()
            .WithWebHostBuilder(builder =>
            {
                // Override settings for testing
                builder.UseSetting("SkipDatabaseInitialization", "true");
                // Provide a dummy connection string to satisfy the configuration check
                // This will be replaced with InMemory in ConfigureServices
                builder.UseSetting("ConnectionStrings:postgres-db", "Host=localhost;Database=test;Username=test;Password=test");
                builder.UseSetting("ConnectionStrings:messaging", "amqp://guest:guest@localhost:5672/");
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
                        if (descriptor.ImplementationType == typeof(JaimesDbContext))
                        {
                            toRemove.Add(descriptor);
                        }
                    }
                    
                    // Remove all matching descriptors
                    foreach (ServiceDescriptor descriptor in toRemove)
                    {
                        services.Remove(descriptor);
                    }
                    
                    // Now manually register the DbContext with InMemory
                    // We manually construct and register the options and context to avoid EF Core's
                    // internal provider tracking that causes "multiple providers" errors
                    services.AddSingleton<DbContextOptions<JaimesDbContext>>(sp =>
                    {
                        DbContextOptionsBuilder<JaimesDbContext> optionsBuilder = new();
                        optionsBuilder.UseInMemoryDatabase(dbName, sqlOpts =>
                        {
                            sqlOpts.EnableNullChecks();
                        });
                        return optionsBuilder.Options;
                    });
                    
                    services.AddScoped<JaimesDbContext>(sp =>
                    {
                        DbContextOptions<JaimesDbContext> options = sp.GetRequiredService<DbContextOptions<JaimesDbContext>>();
                        return new JaimesDbContext(options);
                    });
                    
                    // Replace IRulesSearchService with mock
                    ServiceDescriptor? rulesSearchDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IRulesSearchService));
                    if (rulesSearchDescriptor != null)
                    {
                        services.Remove(rulesSearchDescriptor);
                    }
                    services.AddSingleton(_mockRulesSearchService.Object);
                    
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

    [Fact]
    public async Task SearchRulesEndpoint_WithValidQuery_ReturnsResults()
    {
        // Arrange
        SearchRuleResult[] expectedResults = new[]
        {
            new SearchRuleResult
            {
                Text = "Combat rules for melee attacks",
                DocumentId = "doc1",
                DocumentName = "Player's Handbook.pdf",
                RulesetId = "dnd5e",
                EmbeddingId = "12345",
                ChunkId = "chunk1",
                Relevancy = 0.95
            },
            new SearchRuleResult
            {
                Text = "Ranged combat mechanics",
                DocumentId = "doc2",
                DocumentName = "DM Guide.pdf",
                RulesetId = "dnd5e",
                EmbeddingId = "12346",
                ChunkId = "chunk2",
                Relevancy = 0.87
            }
        };

        _mockRulesSearchService
            .Setup(s => s.SearchRulesDetailedAsync(null, "combat", It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchRulesResponse { Results = expectedResults });

        SearchRulesRequest request = new()
        {
            Query = "combat"
        };

        // Act
        HttpResponseMessage response = await Client.PostAsJsonAsync("/rules/search", request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        SearchRulesResponse? payload = await response.Content.ReadFromJsonAsync<SearchRulesResponse>(cancellationToken: TestContext.Current.CancellationToken);
        payload.ShouldNotBeNull();
        payload.Results.ShouldNotBeNull();
        payload.Results.Length.ShouldBe(2);
        payload.Results[0].Text.ShouldBe("Combat rules for melee attacks");
        payload.Results[0].DocumentName.ShouldBe("Player's Handbook.pdf");
        payload.Results[0].RulesetId.ShouldBe("dnd5e");
        payload.Results[0].Relevancy.ShouldBe(0.95);
        payload.Results[1].Relevancy.ShouldBe(0.87);
    }

    [Fact]
    public async Task SearchRulesEndpoint_WithRulesetFilter_ReturnsFilteredResults()
    {
        // Arrange
        SearchRuleResult[] expectedResults = new[]
        {
            new SearchRuleResult
            {
                Text = "D&D 5e spell casting rules",
                DocumentId = "doc3",
                DocumentName = "Player's Handbook.pdf",
                RulesetId = "dnd5e",
                EmbeddingId = "12347",
                ChunkId = "chunk3",
                Relevancy = 0.92
            }
        };

        _mockRulesSearchService
            .Setup(s => s.SearchRulesDetailedAsync("dnd5e", "spell casting", It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchRulesResponse { Results = expectedResults });

        SearchRulesRequest request = new()
        {
            Query = "spell casting",
            RulesetId = "dnd5e"
        };

        // Act
        HttpResponseMessage response = await Client.PostAsJsonAsync("/rules/search", request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        SearchRulesResponse? payload = await response.Content.ReadFromJsonAsync<SearchRulesResponse>(cancellationToken: TestContext.Current.CancellationToken);
        payload.ShouldNotBeNull();
        payload.Results.Length.ShouldBe(1);
        payload.Results[0].Text.ShouldBe("D&D 5e spell casting rules");
        payload.Results[0].DocumentId.ShouldBe("doc3");
        payload.Results[0].DocumentName.ShouldBe("Player's Handbook.pdf");
        payload.Results[0].RulesetId.ShouldBe("dnd5e");
    }

    [Fact]
    public async Task SearchRulesEndpoint_WithEmptyQuery_ReturnsBadRequest()
    {
        // Arrange
        SearchRulesRequest request = new()
        {
            Query = ""
        };

        // Act
        HttpResponseMessage response = await Client.PostAsJsonAsync("/rules/search", request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SearchRulesEndpoint_WithWhitespaceQuery_ReturnsBadRequest()
    {
        // Arrange
        SearchRulesRequest request = new()
        {
            Query = "   "
        };

        // Act
        HttpResponseMessage response = await Client.PostAsJsonAsync("/rules/search", request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SearchRulesEndpoint_ReturnsTop5ResultsOrderedByRelevancy()
    {
        // Arrange
        SearchRuleResult[] expectedResults = new[]
        {
            new SearchRuleResult { Text = "Result 1", DocumentId = "doc1", DocumentName = "Doc1.pdf", RulesetId = "dnd5e", EmbeddingId = "1", ChunkId = "chunk1", Relevancy = 0.99 },
            new SearchRuleResult { Text = "Result 2", DocumentId = "doc2", DocumentName = "Doc2.pdf", RulesetId = "dnd5e", EmbeddingId = "2", ChunkId = "chunk2", Relevancy = 0.95 },
            new SearchRuleResult { Text = "Result 3", DocumentId = "doc3", DocumentName = "Doc3.pdf", RulesetId = "dnd5e", EmbeddingId = "3", ChunkId = "chunk3", Relevancy = 0.90 },
            new SearchRuleResult { Text = "Result 4", DocumentId = "doc4", DocumentName = "Doc4.pdf", RulesetId = "dnd5e", EmbeddingId = "4", ChunkId = "chunk4", Relevancy = 0.85 },
            new SearchRuleResult { Text = "Result 5", DocumentId = "doc5", DocumentName = "Doc5.pdf", RulesetId = "dnd5e", EmbeddingId = "5", ChunkId = "chunk5", Relevancy = 0.80 }
        };

        _mockRulesSearchService
            .Setup(s => s.SearchRulesDetailedAsync(null, "test", It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchRulesResponse { Results = expectedResults });

        SearchRulesRequest request = new()
        {
            Query = "test"
        };

        // Act
        HttpResponseMessage response = await Client.PostAsJsonAsync("/rules/search", request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        SearchRulesResponse? payload = await response.Content.ReadFromJsonAsync<SearchRulesResponse>(cancellationToken: TestContext.Current.CancellationToken);
        payload.ShouldNotBeNull();
        payload.Results.Length.ShouldBe(5);
        
        // Verify results are ordered by relevancy descending
        payload.Results[0].Relevancy.ShouldBe(0.99);
        payload.Results[1].Relevancy.ShouldBe(0.95);
        payload.Results[2].Relevancy.ShouldBe(0.90);
        payload.Results[3].Relevancy.ShouldBe(0.85);
        payload.Results[4].Relevancy.ShouldBe(0.80);
    }

    [Fact]
    public async Task SearchRulesEndpoint_WithNoResults_ReturnsEmptyArray()
    {
        // Arrange
        _mockRulesSearchService
            .Setup(s => s.SearchRulesDetailedAsync(null, "nonexistent", It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchRulesResponse { Results = Array.Empty<SearchRuleResult>() });

        SearchRulesRequest request = new()
        {
            Query = "nonexistent"
        };

        // Act
        HttpResponseMessage response = await Client.PostAsJsonAsync("/rules/search", request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        SearchRulesResponse? payload = await response.Content.ReadFromJsonAsync<SearchRulesResponse>(cancellationToken: TestContext.Current.CancellationToken);
        payload.ShouldNotBeNull();
        payload.Results.Length.ShouldBe(0);
    }
}

