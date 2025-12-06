using System.Net.Http.Json;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using Shouldly;

namespace MattEland.Jaimes.Tests.Endpoints;

public class ListPlayersEndpointTests : EndpointTestBase
{
    [Fact]
    public async Task ListPlayersEndpoint_ReturnsPlayers()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        HttpResponseMessage response = await Client.GetAsync("/players", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        PlayerListResponse? payload = await response.Content.ReadFromJsonAsync<PlayerListResponse>(ct);
        payload.ShouldNotBeNull();
        payload.Players.ShouldNotBeNull();
        payload.Players.Length.ShouldBeGreaterThan(0);
    }
}