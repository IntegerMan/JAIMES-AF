using MattEland.Jaimes.Domain;
using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints.AgentInstructionVersions;

public class UpdateAgentInstructionVersionByAgentEndpoint : Endpoint<UpdateAgentInstructionVersionRequest, AgentInstructionVersionResponse>
{
    public required IAgentInstructionVersionsService InstructionVersionsService { get; set; }

    public override void Configure()
    {
        Put("/agents/{agentId}/instruction-versions/{id}");
        AllowAnonymous();
        Description(b => b
            .Produces<AgentInstructionVersionResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Agent Instruction Versions"));
    }

    public override async Task HandleAsync(UpdateAgentInstructionVersionRequest req, CancellationToken ct)
    {
        string? agentId = Route<string>("agentId", true);
        int id = Route<int>("id");

        if (string.IsNullOrEmpty(agentId) || id <= 0)
        {
            ThrowError("Agent ID and Instruction version ID are required");
            return;
        }

        try
        {
            AgentInstructionVersionDto version = await InstructionVersionsService.UpdateInstructionVersionAsync(
                id, req.VersionNumber, req.Instructions, req.IsActive, ct);

            // Verify the version belongs to the agent
            if (version.AgentId != agentId)
            {
                await Send.NotFoundAsync(ct);
                return;
            }

            AgentInstructionVersionResponse response = new()
            {
                Id = version.Id,
                AgentId = version.AgentId,
                VersionNumber = version.VersionNumber,
                Instructions = version.Instructions,
                CreatedAt = version.CreatedAt,
                IsActive = version.IsActive
            };

            await Send.OkAsync(response, ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message);
        }
    }
}