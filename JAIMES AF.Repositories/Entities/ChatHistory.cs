namespace MattEland.Jaimes.Repositories.Entities;

public class ChatHistory
{
    public Guid Id { get; set; }
    public Guid GameId { get; set; }
    public required string ThreadJson { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? PreviousHistoryId { get; set; }
    public int? MessageId { get; set; }

    public Game? Game { get; set; }
    public ChatHistory? PreviousHistory { get; set; }
    public Message? Message { get; set; }
}

