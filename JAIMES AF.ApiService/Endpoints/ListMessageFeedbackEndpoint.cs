using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints;

public class ListMessageFeedbackEndpoint : EndpointWithoutRequest<FeedbackListResponse>
{
    public required IMessageFeedbackService MessageFeedbackService { get; set; }

    public override void Configure()
    {
        Get("/admin/feedback");
        AllowAnonymous(); // Currently internal tool, so anonymous is effectively "anyone with access to the app"
        Description(b => b
            .Produces<FeedbackListResponse>(StatusCodes.Status200OK)
            .WithTags("Feedback"));
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
            ToolName = Query<string?>("toolName", false),
            IsPositive = Query<bool?>("isPositive", false),
            AgentId = Query<string?>("agentId", false),
            InstructionVersionId = Query<int?>("instructionVersionId", false),
            GameId = Query<Guid?>("gameId", false)
        };

        FeedbackListResponse response =
            await MessageFeedbackService.GetFeedbackListAsync(page, pageSize, filters, ct);
        await Send.OkAsync(response, ct);
    }
}

