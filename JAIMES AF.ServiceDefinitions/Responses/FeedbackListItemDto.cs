namespace MattEland.Jaimes.ServiceDefinitions.Responses;

public class FeedbackListItemDto
{
    public int Id { get; set; }
    public int MessageId { get; set; }
    public bool IsPositive { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? InstructionVersionId { get; set; }
    public string? AgentVersion { get; set; }
    
    // Context information
    public Guid GameId { get; set; }
    public string? GamePlayerName { get; set; }
    public string? GameScenarioName { get; set; }
    public string? GameRulesetId { get; set; }
}
