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

    // Reference to the agent and instruction version used to generate this message
    public string? AgentId { get; set; }
    public Agent? Agent { get; set; }
    public int? InstructionVersionId { get; set; }
    public AgentInstructionVersion? InstructionVersion { get; set; }

    public Game? Game { get; set; }
    public Guid? ChatHistoryId { get; set; }
    public ChatHistory? ChatHistory { get; set; }
}