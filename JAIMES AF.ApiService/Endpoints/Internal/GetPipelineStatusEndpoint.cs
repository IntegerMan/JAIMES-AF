using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints.Internal;

/// <summary>
/// Endpoint to get the current document processing pipeline status.
/// </summary>
public class GetPipelineStatusEndpoint(IPipelineStatusService pipelineStatusService)
    : Ep.NoReq.Res<PipelineStatusNotification>
{
    public override void Configure()
    {
        Get("/admin/pipeline-status");
        AllowAnonymous();
        Description(b => b
            .Produces<PipelineStatusNotification>()
            .WithTags("Admin")
            .WithSummary("Get the current document processing pipeline status including queue sizes"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        Logger.LogInformation("Fetching pipeline status");

        PipelineStatusNotification status = await pipelineStatusService.GetCurrentStatusAsync(ct);

        await Send.OkAsync(status, ct);
    }
}
