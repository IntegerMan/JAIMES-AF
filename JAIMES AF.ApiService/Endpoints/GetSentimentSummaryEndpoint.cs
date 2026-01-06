using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints;

/// <summary>
/// Endpoint to get sentiment summary statistics.
/// </summary>
public class GetSentimentSummaryEndpoint : EndpointWithoutRequest<SentimentSummaryResponse>
{
    public required IMessageSentimentService SentimentService { get; set; }

    public override void Configure()
    {
        Get("/admin/sentiments/summary");
        AllowAnonymous();
        Description(b => b
            .Produces<SentimentSummaryResponse>(StatusCodes.Status200OK)
            .WithTags("Admin"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        SentimentSummaryResponse response = await SentimentService.GetSentimentSummaryAsync(ct);
        await Send.OkAsync(response, ct);
    }
}
