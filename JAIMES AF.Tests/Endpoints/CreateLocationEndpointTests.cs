using MattEland.Jaimes.ServiceDefinitions.Responses;
using System.Net.Http.Json;

namespace MattEland.Jaimes.Tests.Endpoints;

public class CreateLocationEndpointTests : EndpointTestBase
{
    [Fact]
    public async Task CreateLocation_WithValidData_ReturnsCreated()
    {
        // Arrange
        CancellationToken ct = TestContext.Current.CancellationToken;
        string gameId = Guid.NewGuid().ToString();
        await CreateTestGameAsync(gameId, ct);

        var request = new
        {
            Name = "New Location",
            Description = "A description of the new location"
        };

        // Act
        HttpResponseMessage response = await Client.PostAsJsonAsync($"/games/{gameId}/locations", request, ct);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        LocationResponse? location = await response.Content.ReadFromJsonAsync<LocationResponse>(ct);
        location.ShouldNotBeNull();
        location.Name.ShouldBe(request.Name);
        location.Description.ShouldBe(request.Description);
        location.Id.ShouldBeGreaterThan(0);

        response.Headers.Location.ShouldNotBeNull();
        response.Headers.Location.ToString().ShouldContain($"/locations/{location.Id}");
    }

    [Fact]
    public async Task CreateLocation_WithNonExistentGame_ReturnsNotFound()
    {
        // Arrange
        CancellationToken ct = TestContext.Current.CancellationToken;
        string gameId = Guid.NewGuid().ToString(); // Game not created

        var request = new
        {
            Name = "New Location",
            Description = "A description"
        };

        // Act
        HttpResponseMessage response = await Client.PostAsJsonAsync($"/games/{gameId}/locations", request, ct);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateLocation_WithInvalidData_ReturnsBadRequest()
    {
        // Arrange
        CancellationToken ct = TestContext.Current.CancellationToken;
        string gameId = Guid.NewGuid().ToString();
        await CreateTestGameAsync(gameId, ct);

        var request = new
        {
            Name = "", // Invalid: empty name
            Description = "A description"
        };

        // Act
        HttpResponseMessage response = await Client.PostAsJsonAsync($"/games/{gameId}/locations", request, ct);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateLocation_WithDuplicateName_ReturnsConflict()
    {
        // Arrange
        CancellationToken ct = TestContext.Current.CancellationToken;
        string gameId = Guid.NewGuid().ToString();
        await CreateTestGameAsync(gameId, ct);

        var request = new
        {
            Name = "Duplicate Location",
            Description = "A description"
        };

        // Create the first location
        await Client.PostAsJsonAsync($"/games/{gameId}/locations", request, ct);

        // Act - Try to create it again
        HttpResponseMessage response = await Client.PostAsJsonAsync($"/games/{gameId}/locations", request, ct);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    private async Task CreateTestGameAsync(string gameId, CancellationToken ct)
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<JaimesDbContext>();

        context.Games.Add(new Game
        {
            Id = Guid.Parse(gameId),
            Title = "Test Game",
            RulesetId = "test-ruleset",
            ScenarioId = "test-scenario",
            PlayerId = "test-player",
            CreatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync(ct);
    }
}
