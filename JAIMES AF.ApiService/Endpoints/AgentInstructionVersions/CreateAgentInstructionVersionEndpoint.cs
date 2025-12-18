using MattEland.Jaimes.Domain;
using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints.AgentInstructionVersions;

public class CreateAgentInstructionVersionEndpoint : Endpoint<CreateAgentInstructionVersionRequest, AgentInstructionVersionResponse>
{
    public required IAgentInstructionVersionsService InstructionVersionsService { get; set; }

    public override void Configure()
    {
        Post("/agents/{agentId}/instruction-versions");
        AllowAnonymous();
        Description(b => b
            .Produces<AgentInstructionVersionResponse>(StatusCodes.Status200OK)
            .WithTags("Agent Instruction Versions"));
    }

    public override async Task HandleAsync(CreateAgentInstructionVersionRequest req, CancellationToken ct)
    {
        string? agentId = Route<string>("agentId", true);
        if (string.IsNullOrEmpty(agentId))
        {
            ThrowError("Agent ID is required");
            return;
        }

        try
        {
            AgentInstructionVersionDto version = await InstructionVersionsService.CreateInstructionVersionAsync(
                agentId, req.VersionNumber, req.Instructions, ct);

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
