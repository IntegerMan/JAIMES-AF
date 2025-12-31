using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints;

/// <summary>
/// Endpoint to list sentiment records with optional filtering.
/// </summary>
public class ListMessageSentimentsEndpoint : EndpointWithoutRequest<SentimentListResponse>
{
    public required IMessageSentimentService SentimentService { get; set; }

    public override void Configure()
    {
        Get("/admin/sentiments");
        AllowAnonymous();
        Description(b => b
            .Produces<SentimentListResponse>(StatusCodes.Status200OK)
            .WithTags("Admin"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        int page = Query<int>("page", false);
        if (page < 1) page = 1;

        int pageSize = Query<int>("pageSize", false);
        if (pageSize < 1) pageSize = 20;

        // Build filter params from query string
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

        SentimentListResponse response =
            await SentimentService.GetSentimentListAsync(page, pageSize, filters, ct);

        await Send.OkAsync(response, ct);
    }
}
