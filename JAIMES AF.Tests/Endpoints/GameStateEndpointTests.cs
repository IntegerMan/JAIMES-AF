using System.Net.Http.Json;
using MattEland.Jaimes.ApiService.Requests;
using MattEland.Jaimes.ApiService.Responses;
using Shouldly;

namespace MattEland.Jaimes.Tests.Endpoints;

public class GameStateEndpointTests : EndpointTestBase
{
    [Fact]
    public async Task GameStateEndpoint_ReturnsGame_WhenGameExists()
    {
        // Arrange - Create a game first
        NewGameRequest createRequest = new NewGameRequest
        {
            ScenarioId = "test-scenario",
            PlayerId = "test-player"
        };
        HttpResponseMessage createResponse = await Client.PostAsJsonAsync("/games/", createRequest);
        NewGameResponse? createdGame = await createResponse.Content.ReadFromJsonAsync<NewGameResponse>();
        createdGame.ShouldNotBeNull();

        // Act - Retrieve the game
        HttpResponseMessage response = await Client.GetAsync($"/games/{createdGame.GameId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        GameStateResponse? game = await response.Content.ReadFromJsonAsync<GameStateResponse>();
        game.ShouldNotBeNull();
        game.GameId.ShouldBe(createdGame.GameId);
        game.Messages.ShouldNotBeNull();
        game.Messages.ShouldHaveSingleItem();
        game.Messages[0].Text.ShouldBe("Hello World");
    }

    [Fact]
    public async Task GameStateEndpoint_ReturnsNotFound_WhenGameDoesNotExist()
    {
        // Arrange
        Guid nonExistentGameId = Guid.NewGuid();

        // Act
        HttpResponseMessage response = await Client.GetAsync($"/games/{nonExistentGameId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GameStateEndpoint_ReturnsBadRequest_WithInvalidGuid()
    {
        // Act
        HttpResponseMessage response = await Client.GetAsync("/games/not-a-guid");

        // Assert
        // FastEndpoints routing with {gameId:guid} constraint returns NotFound for invalid GUIDs
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MultipleGames_CanBeCreatedAndRetrieved()
    {
        // Arrange & Act - Create multiple games using the default test data
        NewGameRequest game1Request = new NewGameRequest
        {
            ScenarioId = "test-scenario",
            PlayerId = "test-player"
        };
        HttpResponseMessage game1Response = await Client.PostAsJsonAsync("/games/", game1Request);
        NewGameResponse? game1 = await game1Response.Content.ReadFromJsonAsync<NewGameResponse>();

        NewGameRequest game2Request = new NewGameRequest
        {
            ScenarioId = "test-scenario",
            PlayerId = "test-player"
        };
        HttpResponseMessage game2Response = await Client.PostAsJsonAsync("/games/", game2Request);
        NewGameResponse? game2 = await game2Response.Content.ReadFromJsonAsync<NewGameResponse>();

        game1.ShouldNotBeNull();
        game2.ShouldNotBeNull();
        game1.GameId.ShouldNotBe(game2.GameId);

        // Assert - Both games can be retrieved independently
        HttpResponseMessage retrieveGame1Response = await Client.GetAsync($"/games/{game1.GameId}");
        GameStateResponse? retrievedGame1 = await retrieveGame1Response.Content.ReadFromJsonAsync<GameStateResponse>();
        retrievedGame1.ShouldNotBeNull();
        retrievedGame1.GameId.ShouldBe(game1.GameId);

        HttpResponseMessage retrieveGame2Response = await Client.GetAsync($"/games/{game2.GameId}");
        GameStateResponse? retrievedGame2 = await retrieveGame2Response.Content.ReadFromJsonAsync<GameStateResponse>();
        retrievedGame2.ShouldNotBeNull();
        retrievedGame2.GameId.ShouldBe(game2.GameId);
    }
}
