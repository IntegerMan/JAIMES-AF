namespace MattEland.Jaimes.ServiceDefinitions.Responses;

public record MessageResponse
{
    public required string Text { get; set; }
    public ChatParticipant Participant { get; set; }
    public string? PlayerId { get; set; }
    public required string ParticipantName { get; set; }
    public DateTime CreatedAt { get; set; }
}