using MattEland.Jaimes.ApiService.Responses;
using System.Net.Http.Json;
using Shouldly;

namespace MattEland.Jaimes.Tests.Endpoints;

public class ListRulesetsEndpointTests : EndpointTestBase
{
 [Fact]
 public async Task ListRulesetsEndpoint_ReturnsRulesets()
 {
 HttpResponseMessage response = await Client.GetAsync("/rulesets");
 response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);

 RulesetListResponse? payload = await response.Content.ReadFromJsonAsync<RulesetListResponse>();
 payload.ShouldNotBeNull();
 payload.Rulesets.ShouldNotBeNull();
 payload.Rulesets.Length.ShouldBeGreaterThan(0);
 }
}
