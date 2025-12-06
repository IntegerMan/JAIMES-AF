using System.Net.Http.Json;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using Shouldly;

namespace MattEland.Jaimes.Tests.Endpoints;

public class ListRulesetsEndpointTests : EndpointTestBase
{
    [Fact]
    public async Task ListRulesetsEndpoint_ReturnsRulesets()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        HttpResponseMessage response = await Client.GetAsync("/rulesets", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        RulesetListResponse? payload = await response.Content.ReadFromJsonAsync<RulesetListResponse>(ct);
        payload.ShouldNotBeNull();
        payload.Rulesets.ShouldNotBeNull();
        payload.Rulesets.Length.ShouldBeGreaterThan(0);
    }
}