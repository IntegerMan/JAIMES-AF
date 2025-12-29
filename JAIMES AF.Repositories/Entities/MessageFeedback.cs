namespace MattEland.Jaimes.Repositories.Entities;

public class MessageFeedback
{
    public int Id { get; set; }
    public int MessageId { get; set; }
    public bool IsPositive { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? InstructionVersionId { get; set; }

    // Navigation properties
    public Message? Message { get; set; }
    public AgentInstructionVersion? InstructionVersion { get; set; }
}

