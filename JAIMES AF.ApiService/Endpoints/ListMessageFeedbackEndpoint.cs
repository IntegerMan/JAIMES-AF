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

        string? toolName = Query<string?>("toolName", false);
        bool? isPositive = Query<bool?>("isPositive", false);

        FeedbackListResponse response =
            await MessageFeedbackService.GetFeedbackListAsync(page, pageSize, toolName, isPositive, ct);
        await Send.OkAsync(response, ct);
    }
}
