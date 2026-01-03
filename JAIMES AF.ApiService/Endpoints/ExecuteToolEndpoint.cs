using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints;

/// <summary>
/// Endpoint for executing a tool for testing purposes.
/// </summary>
public class ExecuteToolEndpoint : Ep.Req<ToolExecutionRequest>.Res<ToolExecutionResponse>
{
    public required IToolTestService ToolTestService { get; set; }

    public override void Configure()
    {
        Post("/admin/tools/execute");
        AllowAnonymous();
        Description(b => b
            .Produces<ToolExecutionResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .WithTags("Admin"));
    }

    public override async Task HandleAsync(ToolExecutionRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.ToolName))
        {
            ThrowError("Tool name is required.");
            return;
        }

        ToolExecutionResponse response = await ToolTestService.ExecuteToolAsync(req, ct);
        await Send.OkAsync(response, ct);
    }
}
