using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints;

public class AgentMessagesEndpoint : EndpointWithoutRequest<IEnumerable<MessageContextDto>>
{
    public required IMessageService MessageService { get; set; }

    public override void Configure()
    {
        Get("/admin/messages");
        AllowAnonymous();
        Description(b => b
            .Produces<IEnumerable<MessageContextDto>>(StatusCodes.Status200OK)
            .WithTags("Agents"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        string? agentId = Query<string?>("agentId", false);
        int? versionId = Query<int?>("versionId", false);

        try
        {
            IEnumerable<MessageContextDto> messages =
                await MessageService.GetMessagesByAgentAsync(agentId, versionId, ct);
            await Send.OkAsync(messages, ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message);
        }
    }
}
