using FastEndpoints;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints;

public class ListIndexesEndpoint : Ep.NoReq.Res<IndexListResponse>
{
    public required IDocumentListService DocumentListService { get; set; }

    public override void Configure()
    {
        Get("/admin/indexes");
        AllowAnonymous();
        Description(b => b
            .Produces<IndexListResponse>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithTags("Admin"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        try
        {
            IndexListResponse response = await DocumentListService.ListIndexesAsync(ct);
            await Send.OkAsync(response, cancellation: ct);
        }
        catch (Exception ex)
        {
            ThrowError(ex.Message);
        }
    }
}

