using MattEland.Jaimes.Domain;
using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints.AgentInstructionVersions;

public class UpdateAgentInstructionVersionEndpoint : Endpoint<UpdateAgentInstructionVersionRequest, AgentInstructionVersionResponse>
{
    public required IAgentInstructionVersionsService InstructionVersionsService { get; set; }

    public override void Configure()
    {
        Put("/agent-instruction-versions/{id}");
        AllowAnonymous();
        Description(b => b
            .Produces<AgentInstructionVersionResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Agent Instruction Versions"));
    }

    public override async Task HandleAsync(UpdateAgentInstructionVersionRequest req, CancellationToken ct)
    {
        int id = Route<int>("id");
        if (id <= 0)
        {
            ThrowError("Instruction version ID is required");
            return;
        }

        try
        {
            AgentInstructionVersionDto version = await InstructionVersionsService.UpdateInstructionVersionAsync(
                id, req.VersionNumber, req.Instructions, req.IsActive, ct);

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


