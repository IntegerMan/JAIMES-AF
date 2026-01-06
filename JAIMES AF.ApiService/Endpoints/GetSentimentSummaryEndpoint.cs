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
        var filters = new AdminFilterParams
        {
            GameId = Query<Guid?>("gameId", false),
            AgentId = Query<string?>("agentId", false),
            InstructionVersionId = Query<int?>("instructionVersionId", false),
            ToolName = Query<string?>("toolName", false),
            Sentiment = Query<int?>("sentiment", false),
            HasFeedback = Query<bool?>("hasFeedback", false),
            FeedbackType = Query<int?>("feedbackType", false)
        };

        SentimentSummaryResponse response = await SentimentService.GetSentimentSummaryAsync(filters, ct);
        await Send.OkAsync(response, ct);
    }
}
