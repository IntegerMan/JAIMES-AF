namespace MattEland.Jaimes.Repositories.Entities;

public class Game
{
    public Guid Id { get; set; }
    public required string Title { get; set; }
    public required string RulesetId { get; set; }
    public required string ScenarioId { get; set; }
    public required string PlayerId { get; set; }
    public string? AgentId { get; set; }
    public int? InstructionVersionId { get; set; }
    public DateTime CreatedAt { get; set; }
    public Ruleset? Ruleset { get; set; }
    public Scenario? Scenario { get; set; }
    public Player? Player { get; set; }
    public Agent? Agent { get; set; }
    public AgentInstructionVersion? InstructionVersion { get; set; }
    public ICollection<Message> Messages { get; set; } = new List<Message>();
    public Guid? MostRecentHistoryId { get; set; }
    public ChatHistory? MostRecentHistory { get; set; }
}