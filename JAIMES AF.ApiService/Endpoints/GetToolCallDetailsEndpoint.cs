using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints;

/// <summary>
/// Endpoint for retrieving full details of a specific tool call.
/// </summary>
public class GetToolCallDetailsEndpoint : EndpointWithoutRequest<ToolCallFullDetailResponse>
{
    public required IToolUsageService ToolUsageService { get; set; }

    public override void Configure()
    {
        Get("/admin/tool-calls/{Id}");
        AllowAnonymous();
        Description(b => b
            .Produces<ToolCallFullDetailResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Admin"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        int id = Route<int>("Id");

        ToolCallFullDetailResponse? response = await ToolUsageService.GetToolCallFullDetailsAsync(id, ct);

        if (response == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(response, ct);
    }
}
