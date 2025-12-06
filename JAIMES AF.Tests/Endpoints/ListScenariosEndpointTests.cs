using System.Net.Http.Json;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using Shouldly;

namespace MattEland.Jaimes.Tests.Endpoints;

public class ListScenariosEndpointTests : EndpointTestBase
{
    [Fact]
    public async Task ListScenariosEndpoint_ReturnsScenarios()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        HttpResponseMessage response = await Client.GetAsync("/scenarios", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        ScenarioListResponse? payload = await response.Content.ReadFromJsonAsync<ScenarioListResponse>(ct);
        payload.ShouldNotBeNull();
        payload.Scenarios.ShouldNotBeNull();
        payload.Scenarios.Length.ShouldBeGreaterThan(0);
    }
}