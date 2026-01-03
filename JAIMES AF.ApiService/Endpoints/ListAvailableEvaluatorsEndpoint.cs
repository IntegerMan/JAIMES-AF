using MattEland.Jaimes.ServiceDefinitions.Responses;
using Microsoft.Extensions.AI.Evaluation;

namespace MattEland.Jaimes.ApiService.Endpoints;

/// <summary>
/// Endpoint for listing available evaluator names for use in the test evaluator UI.
/// </summary>
public class ListAvailableEvaluatorsEndpoint : EndpointWithoutRequest<AvailableEvaluatorsResponse>
{
    public required IEnumerable<IEvaluator> Evaluators { get; set; }

    public override void Configure()
    {
        Get("/admin/evaluators/available");
        AllowAnonymous();
        Description(b => b
            .Produces<AvailableEvaluatorsResponse>(StatusCodes.Status200OK)
            .WithTags("Evaluators"));
    }

    public override Task HandleAsync(CancellationToken ct)
    {
        List<string> evaluatorNames = Evaluators
            .Select(e => e.GetType().Name)
            .Distinct()
            .OrderBy(n => n)
            .ToList();

        AvailableEvaluatorsResponse response = new()
        {
            EvaluatorNames = evaluatorNames
        };

        return Send.OkAsync(response, ct);
    }
}
