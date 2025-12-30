using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints;

/// <summary>
/// Endpoint for getting detailed tool call information for a specific tool.
/// </summary>
public class GetToolUsageDetailsEndpoint : EndpointWithoutRequest<ToolCallDetailListResponse>
{
    public required IToolUsageService ToolUsageService { get; set; }

    public override void Configure()
    {
        Get("/admin/tools/{toolName}");
        AllowAnonymous();
        Description(b => b
            .Produces<ToolCallDetailListResponse>(StatusCodes.Status200OK)
            .WithTags("Admin"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        string toolName = Route<string>("toolName") ?? string.Empty;

        int page = Query<int>("page", false);
        if (page < 1) page = 1;

        int pageSize = Query<int>("pageSize", false);
        if (pageSize < 1) pageSize = 20;

        string? agentId = Query<string?>("agentId", false);
        int? instructionVersionId = Query<int?>("instructionVersionId", false);
        Guid? gameId = Query<Guid?>("gameId", false);

        ToolCallDetailListResponse response = await ToolUsageService.GetToolCallDetailsAsync(
            toolName,
            page,
            pageSize,
            agentId,
            instructionVersionId,
            gameId,
            ct);

        await Send.OkAsync(response, ct);
    }
}
