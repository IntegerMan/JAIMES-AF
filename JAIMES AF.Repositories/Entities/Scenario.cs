namespace MattEland.Jaimes.Repositories.Entities;

public class Scenario
{
    public required string Id { get; set; }
    public required string RulesetId { get; set; }
    public string? Description { get; set; }
    public required string Name { get; set; }
    public required string SystemPrompt { get; set; }
    public string? InitialGreeting { get; set; }
    public Ruleset? Ruleset { get; set; }
    public ICollection<Game> Games { get; set; } = new List<Game>();
}