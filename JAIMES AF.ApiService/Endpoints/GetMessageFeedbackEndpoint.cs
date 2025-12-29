using MattEland.Jaimes.Domain;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints;

public class GetMessageFeedbackEndpoint : EndpointWithoutRequest<MessageFeedbackResponse>
{
    public required IMessageFeedbackService MessageFeedbackService { get; set; }

    public override void Configure()
    {
        Get("/messages/{messageId}/feedback");
        AllowAnonymous();
        Description(b => b
            .Produces<MessageFeedbackResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status204NoContent)
            .WithTags("Messages"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        string? messageIdStr = Route<string>("messageId", true);
        if (string.IsNullOrEmpty(messageIdStr) || !int.TryParse(messageIdStr, out int messageId))
        {
            ThrowError("Invalid message ID format");
            return;
        }

        try
        {
            MessageFeedbackDto? feedback = await MessageFeedbackService.GetFeedbackForMessageAsync(messageId, ct);

            if (feedback == null)
            {
                await Send.NoContentAsync(ct);
                return;
            }

            MessageFeedbackResponse response = new()
            {
                Id = feedback.Id,
                MessageId = feedback.MessageId,
                IsPositive = feedback.IsPositive,
                Comment = feedback.Comment,
                CreatedAt = feedback.CreatedAt,
                InstructionVersionId = feedback.InstructionVersionId
            };

            await Send.OkAsync(response, ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message);
        }
    }
}

