using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints;

/// <summary>
/// Endpoint for getting classification model details including training metrics.
/// </summary>
public class GetClassificationModelDetailsEndpoint : EndpointWithoutRequest<ClassificationModelDetailsResponse>
{
    public required IClassificationModelService ClassificationModelService { get; set; }

    public override void Configure()
    {
        Get("/admin/classification-models/{id}");
        AllowAnonymous();
        Description(b => b
            .Produces<ClassificationModelDetailsResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Admin"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        int id = Route<int>("id");
        
        ClassificationModelDetailsResponse? model = await ClassificationModelService.GetModelDetailsAsync(id, ct);
        
        if (model == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(model, ct);
    }
}
