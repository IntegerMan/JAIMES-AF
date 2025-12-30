using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace MattEland.Jaimes.Tests.Repositories;

public class ModelExtensionsTests : RepositoryTestBase
{
    [Fact]
    public async Task GetOrCreateModelAsync_WithTrailingSlash_ShouldReuseExistingModel()
    {
        // Arrange
        string name = "gemma3";
        string provider = "Ollama";
        string endpoint = "http://localhost:11434"; // No slash
        string endpointWithSlash = "http://localhost:11434/";

        // Pre-create the model
        Model model = new()
        {
            Name = name,
            Provider = provider,
            Endpoint = endpoint,
            CreatedAt = DateTime.UtcNow
        };
        Context.Models.Add(model);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        Model? retrieved = await Context.GetOrCreateModelAsync(
            name,
            provider,
            endpointWithSlash,
            null,
            TestContext.Current.CancellationToken);

        // Assert
        retrieved.ShouldNotBeNull();
        retrieved.Id.ShouldBe(model.Id);
        retrieved.Endpoint.ShouldBe(endpoint);
    }

    [Fact]
    public async Task GetOrCreateModelAsync_WithDifferentCasing_ShouldReuseExistingModel()
    {
        // Arrange
        string name = "gemma3";
        string provider = "Ollama";
        string endpoint = "http://LOCALHOST:11434";
        string endpointLower = "http://localhost:11434";

        // Pre-create the model
        Model model = new()
        {
            Name = name,
            Provider = provider,
            Endpoint = endpointLower,
            CreatedAt = DateTime.UtcNow
        };
        Context.Models.Add(model);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        Model? retrieved = await Context.GetOrCreateModelAsync(
            name,
            provider,
            endpoint,
            null,
            TestContext.Current.CancellationToken);

        // Assert
        retrieved.ShouldNotBeNull();
        retrieved.Id.ShouldBe(model.Id);
    }

    [Fact]
    public async Task GetOrCreateModelAsync_WithWhitespace_ShouldReuseExistingModel()
    {
        // Arrange
        string name = " gemma3 ";
        string provider = " Ollama ";
        string endpoint = " http://localhost:11434 ";

        // Pre-create the model
        Model model = new()
        {
            Name = "gemma3",
            Provider = "Ollama",
            Endpoint = "http://localhost:11434",
            CreatedAt = DateTime.UtcNow
        };
        Context.Models.Add(model);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        Model? retrieved = await Context.GetOrCreateModelAsync(
            name,
            provider,
            endpoint,
            null,
            TestContext.Current.CancellationToken);

        // Assert
        retrieved.ShouldNotBeNull();
        retrieved.Id.ShouldBe(model.Id);
    }
}
