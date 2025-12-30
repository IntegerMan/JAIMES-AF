using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints;

/// <summary>
/// Endpoint for listing tool usage statistics.
/// </summary>
public class ListToolUsageEndpoint : EndpointWithoutRequest<ToolUsageListResponse>
{
    public required IToolUsageService ToolUsageService { get; set; }

    public override void Configure()
    {
        Get("/admin/tools");
        AllowAnonymous(); // Currently internal tool, so anonymous is effectively "anyone with access to the app"
        Description(b => b
            .Produces<ToolUsageListResponse>(StatusCodes.Status200OK)
            .WithTags("Admin"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        int page = Query<int>("page", false);
        if (page < 1) page = 1;

        int pageSize = Query<int>("pageSize", false);
        if (pageSize < 1) pageSize = 20;

        string? agentId = Query<string?>("agentId", false);
        int? instructionVersionId = Query<int?>("instructionVersionId", false);
        Guid? gameId = Query<Guid?>("gameId", false);

        ToolUsageListResponse response = await ToolUsageService.GetToolUsageAsync(
            page,
            pageSize,
            agentId,
            instructionVersionId,
            gameId,
            ct);

        await Send.OkAsync(response, ct);
    }
}
