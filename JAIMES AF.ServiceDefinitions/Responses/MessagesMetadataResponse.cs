namespace MattEland.Jaimes.ServiceDefinitions.Responses;

/// <summary>
/// Response containing metadata for multiple messages.
/// </summary>
public record MessagesMetadataResponse
{
    /// <summary>
    /// Feedback for the requested messages, keyed by Message ID.
    /// </summary>
    public Dictionary<int, MessageFeedbackResponse> Feedback { get; init; } = [];

    /// <summary>
    /// Tool calls for the requested messages, keyed by Message ID.
    /// </summary>
    public Dictionary<int, List<MessageToolCallResponse>> ToolCalls { get; init; } = [];

    /// <summary>
    /// Evaluation metrics for the requested messages, keyed by Message ID.
    /// </summary>
    public Dictionary<int, List<MessageEvaluationMetricResponse>> Metrics { get; init; } = [];
    
    /// <summary>
    /// Sentiment information for the requested messages, keyed by Message ID.
    /// </summary>
    public Dictionary<int, MessageSentimentResponse> Sentiment { get; init; } = [];
}

/// <summary>
/// Response structure for message sentiment.
/// </summary>
public record MessageSentimentResponse
{
    public int Sentiment { get; init; }
    public double? Confidence { get; init; }
    public int? SentimentSource { get; init; }
}
