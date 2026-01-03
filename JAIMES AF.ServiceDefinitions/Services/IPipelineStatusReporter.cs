using MattEland.Jaimes.ServiceDefinitions.Requests;

namespace MattEland.Jaimes.ServiceDefinitions.Services;

/// <summary>
/// Service for reporting pipeline queue status to the API service.
/// Used by worker services to periodically report their queue sizes.
/// </summary>
public interface IPipelineStatusReporter
{
    /// <summary>
    /// Reports the current queue size for a pipeline stage.
    /// </summary>
    /// <param name="stage">The pipeline stage (cracking, chunking, or embedding).</param>
    /// <param name="queueSize">The current queue size.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ReportQueueSizeAsync(string stage, int queueSize, CancellationToken cancellationToken = default);
}

/// <summary>
/// HTTP-based implementation of pipeline status reporter.
/// </summary>
public class HttpPipelineStatusReporter : IPipelineStatusReporter
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpPipelineStatusReporter> _logger;
    private readonly string _workerName;

    public HttpPipelineStatusReporter(
        HttpClient httpClient,
        ILogger<HttpPipelineStatusReporter> logger,
        string workerName)
    {
        _httpClient = httpClient;
        _logger = logger;
        _workerName = workerName;
    }

    public async Task ReportQueueSizeAsync(string stage, int queueSize, CancellationToken cancellationToken = default)
    {
        try
        {
            UpdatePipelineQueueSizeRequest request = new()
            {
                Stage = stage,
                QueueSize = queueSize,
                WorkerSource = _workerName
            };

            HttpResponseMessage response = await _httpClient.PostAsJsonAsync("/internal/pipeline-status", request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to report pipeline status: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to report pipeline status to API service");
        }
    }
}
