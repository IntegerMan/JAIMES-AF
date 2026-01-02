using MattEland.Jaimes.Domain;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints.Agents;

public class ListAgentsEndpoint : Ep.NoReq.Res<AgentListResponse>
{
    public required IAgentsService AgentsService { get; set; }

    public override void Configure()
    {
        Get("/agents");
        AllowAnonymous();
        Description(b => b
            .Produces<AgentListResponse>()
            .WithTags("Agents"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        AgentDto[] agents = await AgentsService.GetAgentsAsync(ct);
        await Send.OkAsync(new AgentListResponse
        {
            Agents = agents.Select(a => new AgentResponse
            {
                Id = a.Id,
                Name = a.Name,
                Role = a.Role
            }).ToArray()
        }, ct);
    }
}



