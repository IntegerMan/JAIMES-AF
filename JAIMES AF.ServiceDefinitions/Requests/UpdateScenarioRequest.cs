namespace MattEland.Jaimes.ServiceDefinitions.Requests;

public class UpdateScenarioRequest
{
    public required string RulesetId { get; set; }
    public string? Description { get; set; }
    public required string Name { get; set; }
    public string? ScenarioInstructions { get; set; }
    public string? InitialGreeting { get; set; }
}