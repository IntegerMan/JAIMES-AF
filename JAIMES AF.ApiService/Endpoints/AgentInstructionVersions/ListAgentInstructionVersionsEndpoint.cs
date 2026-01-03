using MattEland.Jaimes.Domain;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints.AgentInstructionVersions;

public class ListAgentInstructionVersionsEndpoint : Ep.NoReq.Res<AgentInstructionVersionListResponse>
{
    public required IAgentInstructionVersionsService InstructionVersionsService { get; set; }

    public override void Configure()
    {
        Get("/agents/{agentId}/instruction-versions");
        AllowAnonymous();
        Description(b => b
            .Produces<AgentInstructionVersionListResponse>()
            .WithTags("Agent Instruction Versions"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        string? agentId = Route<string>("agentId", true);
        if (string.IsNullOrEmpty(agentId))
        {
            ThrowError("Agent ID is required");
            return;
        }

        AgentInstructionVersionDto[] versions =
            await InstructionVersionsService.GetInstructionVersionsAsync(agentId, ct);
        await Send.OkAsync(new AgentInstructionVersionListResponse
        {
            InstructionVersions = versions.Select(v => new AgentInstructionVersionResponse
            {
                Id = v.Id,
                AgentId = v.AgentId,
                VersionNumber = v.VersionNumber,
                Instructions = v.Instructions,
                CreatedAt = v.CreatedAt,
                IsActive = v.IsActive,
                GameCount = v.GameCount,
                LatestGameCount = v.LatestGameCount,
                MessageCount = v.MessageCount
            }).ToArray()
        }, ct);
    }
}



