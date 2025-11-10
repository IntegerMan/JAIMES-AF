namespace MattEland.Jaimes.ServiceDefinitions.Responses;

public record MessageResponse(string Text, ChatParticipant Participant, string? PlayerId, string ParticipantName, DateTime CreatedAt);