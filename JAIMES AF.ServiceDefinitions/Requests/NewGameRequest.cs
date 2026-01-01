namespace MattEland.Jaimes.ServiceDefinitions.Requests;

public class NewGameRequest
{
    public required string ScenarioId { get; init; }
    public string? Title { get; init; }
    public required string PlayerId { get; set; }
}