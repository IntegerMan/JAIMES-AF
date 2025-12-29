using MattEland.Jaimes.Domain;
using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints;

public class SubmitMessageFeedbackEndpoint : Endpoint<SubmitMessageFeedbackRequest, MessageFeedbackResponse>
{
    public required IMessageFeedbackService MessageFeedbackService { get; set; }

    public override void Configure()
    {
        Post("/messages/{messageId}/feedback");
        AllowAnonymous();
        Description(b => b
            .Produces<MessageFeedbackResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Messages"));
    }

    public override async Task HandleAsync(SubmitMessageFeedbackRequest req, CancellationToken ct)
    {
        string? messageIdStr = Route<string>("messageId", true);
        if (string.IsNullOrEmpty(messageIdStr) || !int.TryParse(messageIdStr, out int messageId))
        {
            ThrowError("Invalid message ID format");
            return;
        }

        try
        {
            MessageFeedbackDto feedback = await MessageFeedbackService.SubmitFeedbackAsync(
                messageId,
                req.IsPositive,
                req.Comment,
                ct);

            MessageFeedbackResponse response = new()
            {
                Id = feedback.Id,
                MessageId = feedback.MessageId,
                IsPositive = feedback.IsPositive,
                Comment = feedback.Comment,
                CreatedAt = feedback.CreatedAt,
                InstructionVersionId = feedback.InstructionVersionId
            };

            await Send.CreatedAtAsync<SubmitMessageFeedbackEndpoint>(response, response, Http.POST, cancellation: ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message);
        }
    }
}

