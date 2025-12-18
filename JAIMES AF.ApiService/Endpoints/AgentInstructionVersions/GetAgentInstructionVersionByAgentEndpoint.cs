using MattEland.Jaimes.Domain;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints.AgentInstructionVersions;

public class GetAgentInstructionVersionByAgentEndpoint : EndpointWithoutRequest<AgentInstructionVersionResponse>
{
    public required IAgentInstructionVersionsService InstructionVersionsService { get; set; }

    public override void Configure()
    {
        Get("/agents/{agentId}/instruction-versions/{id}");
        AllowAnonymous();
        Description(b => b
            .Produces<AgentInstructionVersionResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Agent Instruction Versions"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        string? agentId = Route<string>("agentId", true);
        int id = Route<int>("id");

        if (string.IsNullOrEmpty(agentId) || id <= 0)
        {
            ThrowError("Agent ID and Instruction version ID are required");
            return;
        }

        AgentInstructionVersionDto? version = await InstructionVersionsService.GetInstructionVersionAsync(id, ct);
        if (version == null || version.AgentId != agentId)
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
}