using MattEland.Jaimes.Domain;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints;

public class GetMessageToolCallsEndpoint : EndpointWithoutRequest<IEnumerable<MessageToolCallResponse>>
{
    public required IMessageToolCallService MessageToolCallService { get; set; }

    public override void Configure()
    {
        Get("/messages/{messageId}/tool-calls");
        AllowAnonymous();
        Description(b => b
            .Produces<IEnumerable<MessageToolCallResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
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
            IReadOnlyList<MessageToolCallDto> toolCalls = await MessageToolCallService.GetToolCallsForMessageAsync(messageId, ct);

            List<MessageToolCallResponse> responses = toolCalls.Select(tc => new MessageToolCallResponse
            {
                Id = tc.Id,
                MessageId = tc.MessageId,
                ToolName = tc.ToolName,
                InputJson = tc.InputJson,
                OutputJson = tc.OutputJson,
                CreatedAt = tc.CreatedAt,
                InstructionVersionId = tc.InstructionVersionId
            }).ToList();

            await Send.OkAsync(responses, ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message);
        }
    }
}

