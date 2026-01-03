using System.Net;
using System.Net.Http.Json;
using MattEland.Jaimes.ServiceDefinitions.Responses;

namespace MattEland.Jaimes.Tests.Endpoints;

public class ListAvailableEvaluatorsEndpointTests : EndpointTestBase
{
    [Fact]
    public async Task ListAvailableEvaluators_ReturnsRegisteredEvaluators()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        // Call the endpoint
        var httpResponse = await Client.GetAsync("/admin/evaluators/available", ct);
        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await httpResponse.Content.ReadFromJsonAsync<AvailableEvaluatorsResponse>(ct);

        // Verify response structure
        response.ShouldNotBeNull();
        response.EvaluatorNames.ShouldNotBeNull();
        response.EvaluatorNames.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task ListAvailableEvaluators_IncludesExpectedEvaluators()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        // Call the endpoint
        var httpResponse = await Client.GetAsync("/admin/evaluators/available", ct);
        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await httpResponse.Content.ReadFromJsonAsync<AvailableEvaluatorsResponse>(ct);

        // Verify expected evaluators are present
        response.ShouldNotBeNull();
        response.EvaluatorNames.ShouldContain("BrevityEvaluator");
        response.EvaluatorNames.ShouldContain("PlayerAgencyEvaluator");
        response.EvaluatorNames.ShouldContain("GameMechanicsEvaluator");
        response.EvaluatorNames.ShouldContain("StorytellerEvaluator");
        response.EvaluatorNames.ShouldContain("RelevanceTruthAndCompletenessEvaluator");
    }

    [Fact]
    public async Task ListAvailableEvaluators_ReturnsEvaluatorsInAlphabeticalOrder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        // Call the endpoint
        var httpResponse = await Client.GetAsync("/admin/evaluators/available", ct);
        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await httpResponse.Content.ReadFromJsonAsync<AvailableEvaluatorsResponse>(ct);

        // Verify evaluators are sorted alphabetically
        response.ShouldNotBeNull();
        var evaluatorNames = response.EvaluatorNames.ToList();
        var sortedNames = evaluatorNames.OrderBy(n => n).ToList();
        evaluatorNames.ShouldBe(sortedNames);
    }
}
