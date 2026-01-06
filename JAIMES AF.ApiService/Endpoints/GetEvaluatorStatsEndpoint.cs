using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints;

/// <summary>
/// Endpoint for getting evaluator statistics with optional filtering.
/// </summary>
public class GetEvaluatorStatsEndpoint : EndpointWithoutRequest<EvaluatorStatsResponse>
{
    public required IEvaluatorService EvaluatorService { get; set; }

    public override void Configure()
    {
        Get("/admin/evaluators/{evaluatorId}/stats");
        AllowAnonymous();
        Description(b => b
            .Produces<EvaluatorStatsResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Admin"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        int evaluatorId = Route<int>("evaluatorId");
        string? agentId = Query<string?>("agentId", false);
        int? instructionVersionId = Query<int?>("instructionVersionId", false);
        Guid? gameId = Query<Guid?>("gameId", false);

        EvaluatorStatsResponse? response = await EvaluatorService.GetEvaluatorStatsAsync(
            evaluatorId,
            agentId,
            instructionVersionId,
            gameId,
            ct);

        if (response == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(response, ct);
    }
}
