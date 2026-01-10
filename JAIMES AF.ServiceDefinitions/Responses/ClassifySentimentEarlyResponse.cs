namespace MattEland.Jaimes.ServiceDefinitions.Responses;

/// <summary>
/// Response from early sentiment classification request containing a tracking GUID.
/// The client should store this GUID and wait for a SignalR notification with matching GUID.
/// </summary>
public class ClassifySentimentEarlyResponse
{
    /// <summary>
    /// Tracking GUID for correlating the SignalR notification with this request.
    /// </summary>
    public required Guid TrackingGuid { get; set; }
}
