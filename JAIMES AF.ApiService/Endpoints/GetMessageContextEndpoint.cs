using MattEland.Jaimes.Domain;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints;

public class GetMessageContextEndpoint : EndpointWithoutRequest<IEnumerable<MessageContextDto>>
{
    public required IMessageService MessageService { get; set; }

    public override void Configure()
    {
        Get("/messages/{messageId}/context");
        AllowAnonymous();
        Description(b => b
            .Produces<IEnumerable<MessageContextDto>>(StatusCodes.Status200OK)
            .WithTags("Messages"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        int messageId = Route<int>("messageId");
        int countBefore = Query<int>("count", false);
        if (countBefore < 1) countBefore = 5;

        int countAfter = Query<int>("countAfter", false);
        if (countAfter < 0) countAfter = 0;

        try
        {
            IEnumerable<MessageContextDto> messages =
                await MessageService.GetMessageContextAsync(messageId, countBefore, countAfter, ct);
            await Send.OkAsync(messages, ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message);
        }
    }
}
