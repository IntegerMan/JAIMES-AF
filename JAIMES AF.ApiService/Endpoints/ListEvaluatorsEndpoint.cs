using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints;

/// <summary>
/// Endpoint for listing evaluators with aggregate statistics.
/// </summary>
public class ListEvaluatorsEndpoint : EndpointWithoutRequest<EvaluatorListResponse>
{
    public required IEvaluatorService EvaluatorService { get; set; }

    public override void Configure()
    {
        Get("/admin/evaluators");
        AllowAnonymous();
        Description(b => b
            .Produces<EvaluatorListResponse>(StatusCodes.Status200OK)
            .WithTags("Admin"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        int page = Query<int>("page", false);
        if (page < 1) page = 1;

        int pageSize = Query<int>("pageSize", false);
        if (pageSize < 1) pageSize = 20;

        string? agentId = Query<string?>("agentId", false);
        int? instructionVersionId = Query<int?>("instructionVersionId", false);
        Guid? gameId = Query<Guid?>("gameId", false);

        EvaluatorListResponse response = await EvaluatorService.GetEvaluatorsAsync(
            page,
            pageSize,
            agentId,
            instructionVersionId,
            gameId,
            ct);

        await Send.OkAsync(response, ct);
    }
}
