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

    // Navigation properties for message ordering (linked list structure)
    public int? PreviousMessageId { get; set; }
    public Message? PreviousMessage { get; set; }
    public int? NextMessageId { get; set; }
    public Message? NextMessage { get; set; }

    // Navigation property for embedding (optional 1:0..1 relationship)
    public MessageEmbedding? MessageEmbedding { get; set; }

    // Sentiment analysis result: -1 (negative), 0 (neutral), 1 (positive), null (not analyzed)
    public int? Sentiment { get; set; }
}