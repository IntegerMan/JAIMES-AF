namespace MattEland.Jaimes.Repositories.Entities;

public class MessageToolCall
{
    public int Id { get; set; }
    public int MessageId { get; set; }
    public required string ToolName { get; set; }
    public string? InputJson { get; set; }
    public string? OutputJson { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? InstructionVersionId { get; set; }

    public int? ToolId { get; set; }

    // Navigation properties
    public Message? Message { get; set; }
    public AgentInstructionVersion? InstructionVersion { get; set; }
    public Tool? Tool { get; set; }
}



