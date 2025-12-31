using System.Net;
using System.Net.Http.Json;
using MattEland.Jaimes.ServiceDefinitions.Responses;

namespace MattEland.Jaimes.Tests.Endpoints;

public class ListEvaluationMetricsEndpointTests : EndpointTestBase
{
    [Fact]
    public async Task ListEvaluationMetrics_ReturnsEmptyListWhenNoMetrics()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        // Call the endpoint
        var listResponse = await Client.GetAsync("/admin/metrics", ct);
        listResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await listResponse.Content.ReadFromJsonAsync<EvaluationMetricListResponse>(ct);

        // Verify response structure
        response.ShouldNotBeNull();
        response.Items.ShouldNotBeNull();
        response.Page.ShouldBe(1);
        response.PageSize.ShouldBe(20); // Default page size
        response.TotalCount.ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task ListEvaluationMetrics_SupportsPagination()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        // Call endpoint with custom pagination
        var listResponse = await Client.GetAsync("/admin/metrics?page=2&pageSize=5", ct);
        listResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await listResponse.Content.ReadFromJsonAsync<EvaluationMetricListResponse>(ct);

        // Verify pagination was respected
        response.ShouldNotBeNull();
        response.Page.ShouldBe(2);
        response.PageSize.ShouldBe(5);
    }

    [Fact]
    public async Task ListEvaluationMetrics_SupportsFiltering()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        // Call endpoint with filters
        var listResponse = await Client.GetAsync("/admin/metrics?passed=true&metricName=Relevance", ct);
        listResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await listResponse.Content.ReadFromJsonAsync<EvaluationMetricListResponse>(ct);

        // Verify response
        response.ShouldNotBeNull();
        response.Items.ShouldNotBeNull();
        // All returned items should have Passed = true (if any)
        foreach (var item in response.Items)
        {
            item.Passed.ShouldBeTrue();
            item.MetricName.ShouldBe("Relevance", StringCompareShould.IgnoreCase);
        }
    }
}
