namespace MattEland.Jaimes.ServiceDefinitions.Requests;

public class UpdateScenarioRequest
{
    public required string RulesetId { get; set; }
    public string? Description { get; set; }
    public required string Name { get; set; }
    public required string SystemPrompt { get; set; }
    public required string NewGameInstructions { get; set; }
}