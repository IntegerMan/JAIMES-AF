using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints;

/// <summary>
/// Endpoint for activating a classification model.
/// </summary>
public class ActivateClassificationModelEndpoint : EndpointWithoutRequest
{
    public required IClassificationModelService ClassificationModelService { get; set; }

    public override void Configure()
    {
        Post("/admin/classification-models/{id}/activate");
        AllowAnonymous();
        Description(b => b
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Admin"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        int id = Route<int>("id");
        
        bool success = await ClassificationModelService.ActivateModelAsync(id, ct);
        
        if (!success)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(ct);
    }
}
