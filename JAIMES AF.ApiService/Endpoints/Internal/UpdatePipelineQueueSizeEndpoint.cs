using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints.Internal;

/// <summary>
/// Internal endpoint for workers to report their queue sizes.
/// This triggers real-time updates to connected web clients via SignalR.
/// </summary>
public class UpdatePipelineQueueSizeEndpoint(
    IPipelineStatusService pipelineStatusService)
    : Endpoint<UpdatePipelineQueueSizeRequest>
{
    public override void Configure()
    {
        Post("/internal/pipeline-status");
        AllowAnonymous(); // Internal endpoint - workers don't have auth
        Description(d => d
            .WithTags("Internal")
            .Produces(204)
            .WithSummary("Reports queue size for a pipeline stage")
            .WithDescription("Called by worker services to report their current queue size."));
    }

    public override async Task HandleAsync(UpdatePipelineQueueSizeRequest req, CancellationToken ct)
    {
        Logger.LogDebug("Received pipeline status update: {Stage}={QueueSize} from {WorkerSource}",
            req.Stage, req.QueueSize, req.WorkerSource ?? "unknown");

        await pipelineStatusService.UpdateQueueSizeAsync(req.Stage, req.QueueSize, req.WorkerSource, ct);

        await Send.NoContentAsync(ct);
    }
}
