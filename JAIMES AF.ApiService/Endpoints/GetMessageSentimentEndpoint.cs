using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints;

/// <summary>
/// Endpoint to get details for a specific sentiment record.
/// </summary>
public class GetMessageSentimentEndpoint : EndpointWithoutRequest<SentimentFullDetailsResponse>
{
    public required IMessageSentimentService SentimentService { get; set; }

    public override void Configure()
    {
        Get("/admin/sentiments/{id}");
        AllowAnonymous();
        Description(b => b
            .Produces<SentimentFullDetailsResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Admin"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        int id = Route<int>("id");

        SentimentFullDetailsResponse? response = await SentimentService.GetSentimentDetailsAsync(id, ct);

        if (response == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(response, ct);
    }
}
