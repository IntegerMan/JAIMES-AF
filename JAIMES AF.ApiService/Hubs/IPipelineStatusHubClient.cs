using MattEland.Jaimes.ServiceDefinitions.Responses;

namespace MattEland.Jaimes.ApiService.Hubs;

/// <summary>
/// Typed client interface for the PipelineStatusHub.
/// Defines the methods that can be called on connected clients.
/// </summary>
public interface IPipelineStatusHubClient
{
    /// <summary>
    /// Called when the document processing pipeline status has been updated.
    /// </summary>
    /// <param name="notification">The notification with current pipeline queue sizes.</param>
    Task PipelineStatusUpdated(PipelineStatusNotification notification);

    /// <summary>
    /// Called when a message processing stage has been updated.
    /// </summary>
    /// <param name="notification">The notification with current message processing stage.</param>
    Task MessageStageUpdated(MessagePipelineStageNotification notification);
}
