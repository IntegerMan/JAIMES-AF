using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MattEland.Jaimes.ServiceDefaults;
using Shouldly;
using Xunit;

namespace MattEland.Jaimes.Tests.ServiceDefaults;

/// <summary>
/// Integration tests to validate dependency injection configuration.
/// These tests ensure that all required services can be resolved without configuration errors.
/// </summary>
public class DependencyInjectionConfigurationTests
{
    [Fact]
    public void AddChatClient_WithOllamaProvider_ShouldResolveWithoutErrors()
    {
        // Arrange
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "TextGenerationModel:Provider", "Ollama" }
            })
            .Build();

        services.AddLogging();
        services.AddHttpClient();

        // Act & Assert - should not throw
        services.AddChatClient(
            configuration,
            "TextGenerationModel",
            defaultOllamaEndpoint: null, // Simulate missing connection string
            defaultOllamaModel: null);

        // Build service provider to validate configuration
        ServiceProvider serviceProvider = services.BuildServiceProvider();

        // Should be able to resolve IChatClient
        IChatClient chatClient = serviceProvider.GetRequiredService<IChatClient>();
        chatClient.ShouldNotBeNull();
    }

    [Fact]
    public void AddChatClient_WithOllamaProviderAndDefaults_ShouldUseDefaults()
    {
        // Arrange
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "TextGenerationModel:Provider", "Ollama" }
            })
            .Build();

        services.AddLogging();
        services.AddHttpClient();

        // Act
        services.AddChatClient(
            configuration,
            "TextGenerationModel",
            defaultOllamaEndpoint: "http://test-ollama:11434",
            defaultOllamaModel: "test-model");

        // Build service provider to validate configuration
        ServiceProvider serviceProvider = services.BuildServiceProvider();

        // Should be able to resolve IChatClient
        IChatClient chatClient = serviceProvider.GetRequiredService<IChatClient>();
        chatClient.ShouldNotBeNull();
    }

    [Fact]
    public void AddChatClient_WithAzureOpenAIProvider_ShouldRequireEndpointAndName()
    {
        // Arrange
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "TextGenerationModel:Provider", "AzureOpenAI" }
            })
            .Build();

        services.AddLogging();

        // Act
        services.AddChatClient(
            configuration,
            "TextGenerationModel");

        // Build service provider - should throw because endpoint is missing
        ServiceProvider serviceProvider = services.BuildServiceProvider();

        // Assert - should throw when trying to resolve
        Should.Throw<InvalidOperationException>(() => serviceProvider.GetRequiredService<IChatClient>())
            .Message.ShouldContain("endpoint is not configured");
    }

    [Fact]
    public void AddChatClient_WithAzureOpenAIProvider_ShouldResolveWithValidConfiguration()
    {
        // Arrange
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "TextGenerationModel:Provider", "AzureOpenAI" },
                { "TextGenerationModel:Endpoint", "https://test.openai.azure.com" },
                { "TextGenerationModel:Name", "test-deployment" },
                { "TextGenerationModel:Key", "test-key" }
            })
            .Build();

        services.AddLogging();

        // Act
        services.AddChatClient(
            configuration,
            "TextGenerationModel");

        // Build service provider to validate configuration
        ServiceProvider serviceProvider = services.BuildServiceProvider();

        // Should be able to resolve IChatClient
        IChatClient chatClient = serviceProvider.GetRequiredService<IChatClient>();
        chatClient.ShouldNotBeNull();
    }

    [Fact]
    public void AddEmbeddingGenerator_WithOllamaProvider_ShouldResolveWithoutErrors()
    {
        // Arrange
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "EmbeddingModel:Provider", "Ollama" }
            })
            .Build();

        services.AddLogging();

        // Act & Assert - should not throw
        services.AddEmbeddingGenerator(
            configuration,
            "EmbeddingModel",
            defaultOllamaEndpoint: null, // Simulate missing connection string
            defaultOllamaModel: null);

        // Build service provider to validate configuration
        ServiceProvider serviceProvider = services.BuildServiceProvider();

        // Should be able to resolve IEmbeddingGenerator
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator = 
            serviceProvider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
        embeddingGenerator.ShouldNotBeNull();
    }

    [Fact]
    public void AddEmbeddingGenerator_WithAzureOpenAIProvider_ShouldRequireEndpointAndName()
    {
        // Arrange
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "EmbeddingModel:Provider", "AzureOpenAI" }
            })
            .Build();

        services.AddLogging();

        // Act
        services.AddEmbeddingGenerator(
            configuration,
            "EmbeddingModel");

        // Build service provider - should throw because endpoint is missing
        ServiceProvider serviceProvider = services.BuildServiceProvider();

        // Assert - should throw when trying to resolve
        Should.Throw<InvalidOperationException>(() => 
            serviceProvider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>())
            .Message.ShouldContain("endpoint is not configured");
    }

    [Fact]
    public void AddEmbeddingGenerator_WithAzureOpenAIProvider_ShouldResolveWithValidConfiguration()
    {
        // Arrange
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "EmbeddingModel:Provider", "AzureOpenAI" },
                { "EmbeddingModel:Endpoint", "https://test.openai.azure.com" },
                { "EmbeddingModel:Name", "test-deployment" },
                { "EmbeddingModel:Key", "test-key" }
            })
            .Build();

        services.AddLogging();

        // Act
        services.AddEmbeddingGenerator(
            configuration,
            "EmbeddingModel");

        // Build service provider to validate configuration
        ServiceProvider serviceProvider = services.BuildServiceProvider();

        // Should be able to resolve IEmbeddingGenerator
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator = 
            serviceProvider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
        embeddingGenerator.ShouldNotBeNull();
    }
}

