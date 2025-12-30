using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints;

/// <summary>
/// Endpoint to list evaluation metrics with optional filtering.
/// </summary>
public class ListMessageEvaluationMetricsEndpoint : EndpointWithoutRequest<EvaluationMetricListResponse>
{
    public required IMessageEvaluationMetricsService MetricsService { get; set; }

    public override void Configure()
    {
        Get("/admin/metrics");
        AllowAnonymous();
        Description(b => b
            .Produces<EvaluationMetricListResponse>(StatusCodes.Status200OK)
            .WithTags("Admin"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        int page = Query<int>("page", false);
        if (page < 1) page = 1;

        int pageSize = Query<int>("pageSize", false);
        if (pageSize < 1) pageSize = 20;

        // Build filter params from query string
        var filters = new AdminFilterParams
        {
            GameId = Query<Guid?>("gameId", false),
            AgentId = Query<string?>("agentId", false),
            InstructionVersionId = Query<int?>("instructionVersionId", false),
            MetricName = Query<string?>("metricName", false),
            MinScore = Query<double?>("minScore", false),
            MaxScore = Query<double?>("maxScore", false),
            Passed = Query<bool?>("passed", false)
        };

        EvaluationMetricListResponse response =
            await MetricsService.GetMetricsListAsync(page, pageSize, filters, ct);

        await Send.OkAsync(response, ct);
    }
}
