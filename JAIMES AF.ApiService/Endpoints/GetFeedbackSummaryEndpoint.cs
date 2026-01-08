using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints;

/// <summary>
/// Endpoint to get feedback summary statistics.
/// </summary>
public class GetFeedbackSummaryEndpoint : EndpointWithoutRequest<FeedbackSummaryResponse>
{
    public required IMessageFeedbackService FeedbackService { get; set; }

    public override void Configure()
    {
        Get("/admin/feedback/summary");
        AllowAnonymous();
        Description(b => b
            .Produces<FeedbackSummaryResponse>(StatusCodes.Status200OK)
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
            IsPositive = Query<bool?>("isPositive", false)
        };

        FeedbackSummaryResponse response = await FeedbackService.GetFeedbackSummaryAsync(filters, ct);
        await Send.OkAsync(response, ct);
    }
}
