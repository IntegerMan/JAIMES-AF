using FastEndpoints;
using MassTransit;
using MattEland.Jaimes.ServiceDefinitions.Messages;
using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;

namespace MattEland.Jaimes.ApiService.Endpoints;

public class RecrackDocumentEndpoint : Endpoint<RecrackDocumentRequest, DocumentOperationResponse>
{
    public required IPublishEndpoint PublishEndpoint { get; set; }

    public override void Configure()
    {
        Post("/documents/recrack");
        AllowAnonymous();
        Description(b => b
            .Produces<DocumentOperationResponse>()
            .WithTags("Documents")
            .WithSummary("Requests that a specific document be re-cracked"));
    }

    public override async Task HandleAsync(RecrackDocumentRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.FilePath))
        {
            ThrowError("File path is required.");
            return;
        }

        CrackDocumentMessage message = new()
        {
            FilePath = req.FilePath,
            RelativeDirectory = req.RelativeDirectory
        };

        await PublishEndpoint.Publish(message, ct);
        Logger.LogInformation("Requested re-crack for document {FilePath}", req.FilePath);

        DocumentOperationResponse response = new()
        {
            Success = true,
            Message = $"Re-crack requested for {req.FilePath}."
        };

        await Send.OkAsync(response, cancellation: ct);
    }
}

