using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MattEland.Jaimes.ApiService;
using MattEland.Jaimes.ApiService.Requests;
using MattEland.Jaimes.ApiService.Responses;

namespace MattEland.Jaimes.Tests;

public class EndpointTests : IAsyncLifetime
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Use in-memory database for testing
                    services.AddJaimesRepositories("DataSource=:memory:;Cache=Shared");
                });
            });

        _client = _factory.CreateClient();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task NewGameEndpoint_CreatesGame_ReturnsCreated()
    {
        // Arrange
        var request = new NewGameRequest
        {
            RulesetId = "test-ruleset",
            ScenarioId = "test-scenario",
            PlayerId = "test-player"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/games/", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var game = await response.Content.ReadFromJsonAsync<NewGameResponse>();
        Assert.NotNull(game);
        Assert.NotEqual(Guid.Empty, game.GameId);
        Assert.NotNull(game.Messages);
        Assert.Single(game.Messages);
        Assert.Equal("Hello World", game.Messages[0].Text);
    }

    [Fact]
    public async Task NewGameEndpoint_WithMissingFields_ReturnsBadRequest()
    {
        // Arrange
        var request = new { }; // Empty request

        // Act
        var response = await _client.PostAsJsonAsync("/games/", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GameStateEndpoint_ReturnsGame_WhenGameExists()
    {
        // Arrange - Create a game first
        var createRequest = new NewGameRequest
        {
            RulesetId = "test-ruleset",
            ScenarioId = "test-scenario",
            PlayerId = "test-player"
        };
        var createResponse = await _client.PostAsJsonAsync("/games/", createRequest);
        var createdGame = await createResponse.Content.ReadFromJsonAsync<NewGameResponse>();
        Assert.NotNull(createdGame);

        // Act - Retrieve the game
        var response = await _client.GetAsync($"/games/{createdGame.GameId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var game = await response.Content.ReadFromJsonAsync<GameStateResponse>();
        Assert.NotNull(game);
        Assert.Equal(createdGame.GameId, game.GameId);
        Assert.NotNull(game.Messages);
        Assert.Single(game.Messages);
        Assert.Equal("Hello World", game.Messages[0].Text);
    }

    [Fact]
    public async Task GameStateEndpoint_ReturnsNotFound_WhenGameDoesNotExist()
    {
        // Arrange
        var nonExistentGameId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/games/{nonExistentGameId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GameStateEndpoint_ReturnsBadRequest_WithInvalidGuid()
    {
        // Act
        var response = await _client.GetAsync("/games/not-a-guid");

        // Assert
        // FastEndpoints routing with {gameId:guid} constraint returns NotFound for invalid GUIDs
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task MultipleGames_CanBeCreatedAndRetrieved()
    {
        // Arrange & Act - Create multiple games
        var game1Request = new NewGameRequest
        {
            RulesetId = "ruleset-1",
            ScenarioId = "scenario-1",
            PlayerId = "player-1"
        };
        var game1Response = await _client.PostAsJsonAsync("/games/", game1Request);
        var game1 = await game1Response.Content.ReadFromJsonAsync<NewGameResponse>();

        var game2Request = new NewGameRequest
        {
            RulesetId = "ruleset-2",
            ScenarioId = "scenario-2",
            PlayerId = "player-2"
        };
        var game2Response = await _client.PostAsJsonAsync("/games/", game2Request);
        var game2 = await game2Response.Content.ReadFromJsonAsync<NewGameResponse>();

        Assert.NotNull(game1);
        Assert.NotNull(game2);
        Assert.NotEqual(game1.GameId, game2.GameId);

        // Assert - Both games can be retrieved independently
        var retrieveGame1Response = await _client.GetAsync($"/games/{game1.GameId}");
        var retrievedGame1 = await retrieveGame1Response.Content.ReadFromJsonAsync<GameStateResponse>();
        Assert.NotNull(retrievedGame1);
        Assert.Equal(game1.GameId, retrievedGame1.GameId);

        var retrieveGame2Response = await _client.GetAsync($"/games/{game2.GameId}");
        var retrievedGame2 = await retrieveGame2Response.Content.ReadFromJsonAsync<GameStateResponse>();
        Assert.NotNull(retrievedGame2);
        Assert.Equal(game2.GameId, retrievedGame2.GameId);
    }
}
