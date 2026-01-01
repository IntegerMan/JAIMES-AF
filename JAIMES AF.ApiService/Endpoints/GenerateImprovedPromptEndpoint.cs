using FastEndpoints;
using MattEland.Jaimes.ApiService.Services;
using MattEland.Jaimes.Domain;
using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints;

/// <summary>
/// Endpoint for generating an improved prompt based on collected insights.
/// </summary>
public class GenerateImprovedPromptEndpoint : Endpoint<GenerateImprovedPromptRequest, GenerateImprovedPromptResponse>
{
    public required PromptImproverService PromptImproverService { get; set; }
    public required IAgentInstructionVersionsService InstructionVersionsService { get; set; }

    public override void Configure()
    {
        Post("/agents/{agentId}/instruction-versions/{versionId}/generate-improved-prompt");
        AllowAnonymous();
        Description(b => b
            .Produces<GenerateImprovedPromptResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .WithTags("PromptImprover"));
    }

    public override async Task HandleAsync(GenerateImprovedPromptRequest req, CancellationToken ct)
    {
        string? agentId = Route<string>("agentId", true);
        string? versionIdStr = Route<string>("versionId", true);

        if (string.IsNullOrEmpty(agentId))
        {
            ThrowError("Agent ID is required");
            return;
        }

        if (string.IsNullOrEmpty(versionIdStr) || !int.TryParse(versionIdStr, out int versionId))
        {
            ThrowError("Invalid version ID format");
            return;
        }

        AgentInstructionVersionDto?
            version = await InstructionVersionsService.GetInstructionVersionAsync(versionId, ct);
        if (version == null || version.AgentId != agentId)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        if (string.IsNullOrEmpty(req.CurrentPrompt))
        {
            ThrowError("CurrentPrompt is required");
            return;
        }

        GenerateImprovedPromptResponse response = await PromptImproverService.GenerateImprovedPromptAsync(
            agentId,
            versionId,
            req,
            ct);

        await Send.OkAsync(response, ct);
    }
}
