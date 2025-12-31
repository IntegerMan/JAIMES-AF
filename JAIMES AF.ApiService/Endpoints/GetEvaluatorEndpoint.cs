using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints;

/// <summary>
/// Endpoint for getting a single evaluator's details.
/// </summary>
public class GetEvaluatorEndpoint : EndpointWithoutRequest<EvaluatorItemDto>
{
    public required IEvaluatorService EvaluatorService { get; set; }

    public override void Configure()
    {
        Get("/admin/evaluators/{id}");
        AllowAnonymous();
        Description(b => b
            .Produces<EvaluatorItemDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Admin"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        int id = Route<int>("id");

        EvaluatorItemDto? response = await EvaluatorService.GetEvaluatorByIdAsync(id, ct);

        if (response == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(response, ct);
    }
}
