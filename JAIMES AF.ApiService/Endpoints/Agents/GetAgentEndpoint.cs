using MattEland.Jaimes.Domain;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints.Agents;

public class GetAgentEndpoint : EndpointWithoutRequest<AgentResponse>
{
    public required IAgentsService AgentsService { get; set; }

    public override void Configure()
    {
        Get("/agents/{id}");
        AllowAnonymous();
        Description(b => b
            .Produces<AgentResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Agents"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        string? id = Route<string>("id", true);
        if (string.IsNullOrEmpty(id))
        {
            ThrowError("Agent ID is required");
            return;
        }

        AgentDto? agent = await AgentsService.GetAgentAsync(id, ct);
        if (agent == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        AgentResponse response = new()
        {
            Id = agent.Id,
            Name = agent.Name,
            Role = agent.Role
        };

        await Send.OkAsync(response, ct);
    }
}



