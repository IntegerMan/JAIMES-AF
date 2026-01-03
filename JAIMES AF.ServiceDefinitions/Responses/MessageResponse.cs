namespace MattEland.Jaimes.ServiceDefinitions.Responses;

public record MessageResponse
{
    public int Id { get; set; }
    public required string Text { get; set; }
    public ChatParticipant Participant { get; set; }
    public string? PlayerId { get; set; }
    public required string ParticipantName { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? AgentId { get; set; }
    public string? AgentName { get; set; }

    public int? InstructionVersionId { get; set; }
    public string? VersionNumber { get; set; }
    public bool IsScriptedMessage { get; set; }

    // Sentiment analysis result: -1 (negative), 0 (neutral), 1 (positive), null (not analyzed)
    public int? Sentiment { get; set; }

    // Sentiment analysis confidence (0.0 to 1.0), null if not analyzed or legacy data
    public double? SentimentConfidence { get; set; }

    // Source of sentiment: 0 (Model), 1 (Player), null if not analyzed
    public int? SentimentSource { get; set; }

    // Sentiment record ID for navigation
    public int? SentimentId { get; set; }

    public string? ModelName { get; set; }
    public string? ModelProvider { get; set; }
    public string? ModelEndpoint { get; set; }

    /// <summary>
    /// True if this message is missing any registered evaluators.
    /// </summary>
    public bool HasMissingEvaluators { get; set; }

    /// <summary>
    /// True if this message is marked as a test case.
    /// </summary>
    public bool IsTestCase { get; set; }

    /// <summary>
    /// The ID of the test case if this message is a test case, null otherwise.
    /// </summary>
    public int? TestCaseId { get; set; }

    /// <summary>
    /// The expected number of evaluation metrics for this message.
    /// Used for progressive UI updates during streaming evaluation.
    /// Calculated based on registered evaluators (RTC evaluator produces 3 metrics, others produce 1).
    /// </summary>
    public int? ExpectedMetricCount { get; set; }
}