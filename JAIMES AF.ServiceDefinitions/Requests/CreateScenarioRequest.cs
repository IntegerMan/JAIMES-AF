namespace MattEland.Jaimes.ServiceDefinitions.Requests;

public class CreateScenarioRequest
{
    public required string Id { get; set; }
    public required string RulesetId { get; set; }
    public string? Description { get; set; }
    public required string Name { get; set; }
    public string? InitialGreeting { get; set; }
}