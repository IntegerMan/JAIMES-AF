using MattEland.Jaimes.ServiceDefinitions.Responses;

namespace MattEland.Jaimes.ApiService.Hubs;

/// <summary>
/// Typed client interface for the MessageHub.
/// Defines the methods that can be called on connected clients.
/// </summary>
public interface IMessageHubClient
{
    /// <summary>
    /// Called when a message has been updated with new data (sentiment or metrics).
    /// </summary>
    /// <param name="notification">The update notification with message details.</param>
    Task MessageUpdated(MessageUpdateNotification notification);

    /// <summary>
    /// Called when classifier training has completed (successfully or with an error).
    /// </summary>
    /// <param name="notification">The training completion notification.</param>
    Task ClassifierTrainingCompleted(ClassifierTrainingCompletedNotification notification);
}
