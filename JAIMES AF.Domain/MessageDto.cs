namespace MattEland.Jaimes.Domain;

public record MessageDto(string Text, string? PlayerId, string ParticipantName, DateTime CreatedAt);
