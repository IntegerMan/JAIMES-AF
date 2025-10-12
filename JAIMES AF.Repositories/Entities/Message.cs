namespace MattEland.Jaimes.Repositories.Entities;

public class Message
{
    public int Id { get; set; }
    public Guid GameId { get; set; }
    public required string Text { get; set; }
    public DateTime CreatedAt { get; set; }
    public Game? Game { get; set; }
}
