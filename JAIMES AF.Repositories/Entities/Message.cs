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
    public required string AgentId { get; set; }
    public Agent? Agent { get; set; }
    public required int InstructionVersionId { get; set; }
    public AgentInstructionVersion? InstructionVersion { get; set; }

    // Indicates if this message is a scripted message (e.g. system greeting) and exempt from processing
    public bool IsScriptedMessage { get; set; }

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

    // Navigation property for sentiment analysis (optional 1:0..1 relationship)
    public MessageSentiment? MessageSentiment { get; set; }

    // Convenience properties for backward compatibility
    public int? Sentiment => MessageSentiment?.Sentiment;
    public double? SentimentConfidence => MessageSentiment?.Confidence;

    // Reference to the model used to generate this message
    public int? ModelId { get; set; }
    public Model? Model { get; set; }

    // Navigation property for tool calls
    public ICollection<MessageToolCall> ToolCalls { get; set; } = [];
}