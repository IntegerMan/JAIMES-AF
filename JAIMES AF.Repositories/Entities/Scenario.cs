namespace MattEland.Jaimes.Repositories.Entities;

public class Scenario
{
    public required string Id { get; set; }
    public required string RulesetId { get; set; }
    public string? Description { get; set; }
    public required string Name { get; set; }
    public string? ScenarioInstructions { get; set; } // Scenario-specific context (tone, plot, GM notes, etc.)
    public string? InitialGreeting { get; set; }
    public Ruleset? Ruleset { get; set; }
    public ICollection<Game> Games { get; set; } = new List<Game>();
    public ICollection<ScenarioAgent> ScenarioAgents { get; set; } = new List<ScenarioAgent>();
}