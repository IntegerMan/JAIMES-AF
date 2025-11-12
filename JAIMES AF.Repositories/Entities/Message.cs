namespace MattEland.Jaimes.Repositories.Entities;

public class Message
{
    public int Id { get; set; }
    public Guid GameId { get; set; }
    public required string Text { get; set; }
    public DateTime CreatedAt { get; set; }

    // Nullable reference to the player who sent the message. Null means the Game Master.
    public string? PlayerId { get; set; }
    public Player? Player { get; set; }

    public Game? Game { get; set; }
    public Guid? ChatHistoryId { get; set; }
    public ChatHistory? ChatHistory { get; set; }
}
