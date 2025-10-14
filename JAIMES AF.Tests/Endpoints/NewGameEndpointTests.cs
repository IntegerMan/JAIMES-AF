using System.Net.Http.Json;
using MattEland.Jaimes.ApiService.Requests;
using MattEland.Jaimes.ApiService.Responses;
using Shouldly;

namespace MattEland.Jaimes.Tests.Endpoints;

public class NewGameEndpointTests : EndpointTestBase
{
    [Fact]
    public async Task NewGameEndpoint_CreatesGame_ReturnsCreated()
    {
        // Arrange
        NewGameRequest request = new NewGameRequest
        {
            ScenarioId = "test-scenario",
            PlayerId = "test-player"
        };

        // Act
        HttpResponseMessage response = await Client.PostAsJsonAsync("/games/", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        NewGameResponse? game = await response.Content.ReadFromJsonAsync<NewGameResponse>();
        game.ShouldNotBeNull();
        game.GameId.ShouldNotBe(Guid.Empty);
        game.Messages.ShouldNotBeNull();
        game.Messages.ShouldHaveSingleItem();
        game.Messages[0].Text.ShouldBe("Hello World");
    }

    [Fact]
    public async Task NewGameEndpoint_WithMissingFields_ReturnsBadRequest()
    {
        // Arrange
        var request = new { }; // Empty request

        // Act
        HttpResponseMessage response = await Client.PostAsJsonAsync("/games/", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task NewGameEndpoint_WithNonexistentPlayer_ReturnsBadRequest()
    {
        // Arrange
        NewGameRequest request = new NewGameRequest
        {
            ScenarioId = "test-scenario",
            PlayerId = "nonexistent-player"
        };

        // Act
        HttpResponseMessage response = await Client.PostAsJsonAsync("/games/", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task NewGameEndpoint_WithNonexistentScenario_ReturnsBadRequest()
    {
        // Arrange
        NewGameRequest request = new NewGameRequest
        {
            ScenarioId = "nonexistent-scenario",
            PlayerId = "test-player"
        };

        // Act
        HttpResponseMessage response = await Client.PostAsJsonAsync("/games/", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
