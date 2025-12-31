using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints;

public class GetFeedbackDetailsEndpoint : EndpointWithoutRequest<FeedbackFullDetailsResponse>
{
    public required IMessageFeedbackService MessageFeedbackService { get; set; }

    public override void Configure()
    {
        Get("/admin/feedback/{id}");
        AllowAnonymous();
        Description(b => b
            .Produces<FeedbackFullDetailsResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status204NoContent)
            .WithTags("Feedback"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        string? idStr = Route<string>("id", true);
        if (string.IsNullOrEmpty(idStr) || !int.TryParse(idStr, out int id))
        {
            ThrowError("Invalid feedback ID format");
            return;
        }

        try
        {
            FeedbackFullDetailsResponse? feedback = await MessageFeedbackService.GetFeedbackDetailsAsync(id, ct);

            if (feedback == null)
            {
                await Send.NoContentAsync(ct);
                return;
            }

            await Send.OkAsync(feedback, ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message);
        }
    }
}
