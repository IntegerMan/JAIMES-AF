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
        int page = Query<int>("page", isRequired: false) is int p && p > 0 ? p : 1;
        int pageSize = Query<int>("pageSize", isRequired: false) is int ps && ps > 0 && ps <= 1000 ? ps : 50;

        try
        {
            DocumentListResponse response = await DocumentListService.ListDocumentsAsync(indexName, page, pageSize, ct);
            await Send.OkAsync(response, cancellation: ct);
        }
        catch (Exception ex)
        {
            ThrowError(ex.Message);
        }
    }
}

