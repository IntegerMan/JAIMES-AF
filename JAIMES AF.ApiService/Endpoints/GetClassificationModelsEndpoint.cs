using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints;

/// <summary>
/// Endpoint for listing classification models including pending training jobs.
/// </summary>
public class GetClassificationModelsEndpoint : EndpointWithoutRequest<List<ClassificationModelResponse>>
{
    public required IClassificationModelService ClassificationModelService { get; set; }

    public override void Configure()
    {
        Get("/admin/classification-models");
        AllowAnonymous();
        Description(b => b
            .Produces<List<ClassificationModelResponse>>(StatusCodes.Status200OK)
            .WithTags("Admin"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        List<ClassificationModelResponse> models =
            await ClassificationModelService.GetAllModelsWithTrainingJobsAsync(ct);
        await Send.OkAsync(models, ct);
    }
}

