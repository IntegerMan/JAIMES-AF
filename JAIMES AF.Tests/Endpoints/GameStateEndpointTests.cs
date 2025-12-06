using System.Net.Http.Json;
using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using Shouldly;

namespace MattEland.Jaimes.Tests.Endpoints;

public class GameStateEndpointTests : EndpointTestBase
{
    [Fact]
    public async Task GameStateEndpoint_ReturnsGame_WhenGameExists()
    {
        // Arrange - Create a game first
        CancellationToken ct = TestContext.Current.CancellationToken;

        NewGameRequest createRequest = new()
        {
            ScenarioId = "test-scenario",
            PlayerId = "test-player"
        };
        HttpResponseMessage createResponse = await Client.PostAsJsonAsync("/games/", createRequest, ct);
        NewGameResponse? createdGame = await createResponse.Content.ReadFromJsonAsync<NewGameResponse>(ct);
        createdGame.ShouldNotBeNull();

        // Act - Retrieve the game
        HttpResponseMessage response = await Client.GetAsync($"/games/{createdGame.GameId}", ct);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        GameStateResponse? game = await response.Content.ReadFromJsonAsync<GameStateResponse>(ct);
        game.ShouldNotBeNull();
        game.GameId.ShouldBe(createdGame.GameId);
        game.Messages.ShouldNotBeNull();
        game.Messages.ShouldHaveSingleItem();
        game.Messages[0].Text.ShouldNotBeNull();
        game.Messages[0].Text.ShouldNotBeEmpty();
        game.Messages[0].Id.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task GameStateEndpoint_ReturnsNotFound_WhenGameDoesNotExist()
    {
        // Arrange
        CancellationToken ct = TestContext.Current.CancellationToken;
        Guid nonExistentGameId = Guid.NewGuid();

        // Act
        HttpResponseMessage response = await Client.GetAsync($"/games/{nonExistentGameId}", ct);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GameStateEndpoint_ReturnsBadRequest_WithInvalidGuid()
    {
        // Arrange
        CancellationToken ct = TestContext.Current.CancellationToken;

        // Act
        HttpResponseMessage response = await Client.GetAsync("/games/not-a-guid", ct);

        // Assert
        // FastEndpoints routing with {gameId:guid} constraint returns NotFound for invalid GUIDs
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MultipleGames_CanBeCreatedAndRetrieved()
    {
        // Arrange & Act - Create multiple games using the default test data
        CancellationToken ct = TestContext.Current.CancellationToken;

        NewGameRequest game1Request = new()
        {
            ScenarioId = "test-scenario",
            PlayerId = "test-player"
        };
        HttpResponseMessage game1Response = await Client.PostAsJsonAsync("/games/", game1Request, ct);
        NewGameResponse? game1 = await game1Response.Content.ReadFromJsonAsync<NewGameResponse>(ct);

        NewGameRequest game2Request = new()
        {
            ScenarioId = "test-scenario",
            PlayerId = "test-player"
        };
        HttpResponseMessage game2Response = await Client.PostAsJsonAsync("/games/", game2Request, ct);
        NewGameResponse? game2 = await game2Response.Content.ReadFromJsonAsync<NewGameResponse>(ct);

        game1.ShouldNotBeNull();
        game2.ShouldNotBeNull();
        game1.GameId.ShouldNotBe(game2.GameId);

        // Assert - Both games can be retrieved independently
        HttpResponseMessage retrieveGame1Response = await Client.GetAsync($"/games/{game1.GameId}", ct);
        GameStateResponse? retrievedGame1 =
            await retrieveGame1Response.Content.ReadFromJsonAsync<GameStateResponse>(ct);
        retrievedGame1.ShouldNotBeNull();
        retrievedGame1.GameId.ShouldBe(game1.GameId);

        HttpResponseMessage retrieveGame2Response = await Client.GetAsync($"/games/{game2.GameId}", ct);
        GameStateResponse? retrievedGame2 =
            await retrieveGame2Response.Content.ReadFromJsonAsync<GameStateResponse>(ct);
        retrievedGame2.ShouldNotBeNull();
        retrievedGame2.GameId.ShouldBe(game2.GameId);
    }
}