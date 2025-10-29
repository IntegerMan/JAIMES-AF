using MattEland.Jaimes.ApiService.Responses;
using System.Net.Http.Json;
using Shouldly;

namespace MattEland.Jaimes.Tests.Endpoints;

public class ListScenariosEndpointTests : EndpointTestBase
{
 [Fact]
 public async Task ListScenariosEndpoint_ReturnsScenarios()
 {
 HttpResponseMessage response = await Client.GetAsync("/scenarios");
 response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);

 ScenarioListResponse? payload = await response.Content.ReadFromJsonAsync<ScenarioListResponse>();
 payload.ShouldNotBeNull();
 payload.Scenarios.ShouldNotBeNull();
 payload.Scenarios.Length.ShouldBeGreaterThan(0);
 }
}
