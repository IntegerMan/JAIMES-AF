using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints;

/// <summary>
/// Endpoint for getting available tools for testing.
/// </summary>
public class GetAvailableToolsEndpoint : EndpointWithoutRequest<ToolMetadataListResponse>
{
    public required IToolTestService ToolTestService { get; set; }

    public override void Configure()
    {
        Get("/admin/tools/available");
        AllowAnonymous();
        Description(b => b
            .Produces<ToolMetadataListResponse>(StatusCodes.Status200OK)
            .WithTags("Admin"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        ToolMetadataListResponse response = await ToolTestService.GetRegisteredToolsAsync(ct);
        await Send.OkAsync(response, ct);
    }
}
