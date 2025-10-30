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
        CancellationToken ct = TestContext.Current.CancellationToken;
        await Client.PostAsJsonAsync("/games/", createRequest, ct);

        // Act - Retrieve all games
        HttpResponseMessage response = await Client.GetAsync("/games/", ct);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        ListGamesResponse? gamesResponse = await response.Content.ReadFromJsonAsync<ListGamesResponse>(cancellationToken: ct);
        gamesResponse.ShouldNotBeNull();
        gamesResponse.Games.ShouldNotBeNull();
        gamesResponse.Games.Length.ShouldBeGreaterThan(0);
    }
}
