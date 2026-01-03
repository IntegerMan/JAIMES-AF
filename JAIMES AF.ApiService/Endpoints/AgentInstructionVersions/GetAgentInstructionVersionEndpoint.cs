using MattEland.Jaimes.Domain;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints.AgentInstructionVersions;

public class GetAgentInstructionVersionEndpoint : EndpointWithoutRequest<AgentInstructionVersionResponse>
{
    public required IAgentInstructionVersionsService InstructionVersionsService { get; set; }

    public override void Configure()
    {
        Get("/agent-instruction-versions/{id}");
        AllowAnonymous();
        Description(b => b
            .Produces<AgentInstructionVersionResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Agent Instruction Versions"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        int id = Route<int>("id");
        if (id <= 0)
        {
            ThrowError("Instruction version ID is required");
            return;
        }

        AgentInstructionVersionDto? version = await InstructionVersionsService.GetInstructionVersionAsync(id, ct);
        if (version == null)
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



