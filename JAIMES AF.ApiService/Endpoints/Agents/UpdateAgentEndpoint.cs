using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints.Agents;

public class UpdateAgentEndpoint : Endpoint<UpdateAgentRequest, AgentResponse>
{
    public required IAgentsService AgentsService { get; set; }

    public override void Configure()
    {
        Put("/agents/{id}");
        AllowAnonymous();
        Description(b => b
            .Produces<AgentResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Agents"));
    }

    public override async Task HandleAsync(UpdateAgentRequest req, CancellationToken ct)
    {
        string? id = Route<string>("id", true);
        if (string.IsNullOrEmpty(id))
        {
            ThrowError("Agent ID is required");
            return;
        }

        try
        {
            AgentDto agent = await AgentsService.UpdateAgentAsync(id, req.Name, req.Role, ct);

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
