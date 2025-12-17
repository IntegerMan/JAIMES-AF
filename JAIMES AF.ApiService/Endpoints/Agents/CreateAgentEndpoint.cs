using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints.Agents;

public class CreateAgentEndpoint : Endpoint<CreateAgentRequest, AgentResponse>
{
    public required IAgentsService AgentsService { get; set; }

    public override void Configure()
    {
        Post("/agents");
        AllowAnonymous();
        Description(b => b
            .Produces<AgentResponse>(StatusCodes.Status200OK)
            .WithTags("Agents"));
    }

    public override async Task HandleAsync(CreateAgentRequest req, CancellationToken ct)
    {
        try
        {
            AgentDto agent = await AgentsService.CreateAgentAsync(req.Name, req.Role, ct);

            AgentResponse response = new()
            {
                Id = agent.Id,
                Name = agent.Name,
                Role = agent.Role
            };

            await Send.OkAsync(response, ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message);
        }
    }
}
