using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints;

/// <summary>
/// Endpoint for getting training data count based on confidence threshold.
/// </summary>
public class GetTrainingDataCountEndpoint : EndpointWithoutRequest<TrainingDataCountResponse>
{
    public required IClassificationModelService ClassificationModelService { get; set; }

    public override void Configure()
    {
        Get("/admin/classification-models/training-data-count");
        AllowAnonymous();
        Description(b => b
            .Produces<TrainingDataCountResponse>(StatusCodes.Status200OK)
            .WithTags("Admin"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        double? minConfidenceParam = Query<double?>("minConfidence", false);
        double minConfidence = minConfidenceParam ?? 0.75;
        
        TrainingDataCountResponse result = await ClassificationModelService.GetTrainingDataCountAsync(minConfidence, ct);
        await Send.OkAsync(result, ct);
    }
}
