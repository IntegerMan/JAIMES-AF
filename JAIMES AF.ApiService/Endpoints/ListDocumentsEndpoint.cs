using FastEndpoints;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints;

public class ListDocumentsEndpoint : Ep.NoReq.Res<DocumentListResponse>
{
    public required IDocumentListService DocumentListService { get; set; }

    public override void Configure()
    {
        Get("/admin/documents");
        AllowAnonymous();
        Description(b => b
            .Produces<DocumentListResponse>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithTags("Admin"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        string? indexName = Query<string>("index", isRequired: false);

        try
        {
            DocumentListResponse response = await DocumentListService.ListDocumentsAsync(indexName, ct);
            await Send.OkAsync(response, cancellation: ct);
        }
        catch (Exception ex)
        {
            ThrowError(ex.Message);
        }
    }
}

