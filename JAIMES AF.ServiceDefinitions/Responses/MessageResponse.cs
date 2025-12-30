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

    public int? InstructionVersionId { get; set; }

    // Sentiment analysis result: -1 (negative), 0 (neutral), 1 (positive), null (not analyzed)
    public int? Sentiment { get; set; }

    // Sentiment analysis confidence (0.0 to 1.0), null if not analyzed or legacy data
    public double? SentimentConfidence { get; set; }

    // Source of sentiment: 0 (Model), 1 (Player), null if not analyzed
    public int? SentimentSource { get; set; }
    public string? ModelName { get; set; }
    public string? ModelProvider { get; set; }
    public string? ModelEndpoint { get; set; }
}