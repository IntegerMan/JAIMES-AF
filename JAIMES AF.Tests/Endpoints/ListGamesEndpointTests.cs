using MattEland.Jaimes.ApiService.Responses;
using System.Net.Http.Json;
using Shouldly;

namespace MattEland.Jaimes.Tests.Endpoints;

public class ListGamesEndpointTests : EndpointTestBase
{
    [Fact]
    public async Task ListGamesEndpoint_ReturnsGameInfo()
    {
        // Arrange - Create a game first
        var createRequest = new { ScenarioId = "test-scenario", PlayerId = "test-player" };
        await Client.PostAsJsonAsync("/games/", createRequest);

        // Act - Retrieve all games
        HttpResponseMessage response = await Client.GetAsync("/games/");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        ListGamesResponse? gamesResponse = await response.Content.ReadFromJsonAsync<ListGamesResponse>();
        gamesResponse.ShouldNotBeNull();
        gamesResponse.Games.ShouldNotBeNull();
        gamesResponse.Games.Length.ShouldBeGreaterThan(0);
    }
}
