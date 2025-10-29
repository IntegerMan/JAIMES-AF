using MattEland.Jaimes.ApiService.Responses;
using System.Net.Http.Json;
using Shouldly;

namespace MattEland.Jaimes.Tests.Endpoints;

public class ListPlayersEndpointTests : EndpointTestBase
{
 [Fact]
 public async Task ListPlayersEndpoint_ReturnsPlayers()
 {
 HttpResponseMessage response = await Client.GetAsync("/players");
 response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);

 PlayerListResponse? payload = await response.Content.ReadFromJsonAsync<PlayerListResponse>();
 payload.ShouldNotBeNull();
 payload.Players.ShouldNotBeNull();
 payload.Players.Length.ShouldBeGreaterThan(0);
 }
}
