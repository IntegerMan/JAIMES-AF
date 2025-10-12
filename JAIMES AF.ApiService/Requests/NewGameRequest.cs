namespace MattEland.Jaimes.ApiService.Requests;

public class NewGameRequest
{
    public required string RulesetId { get; set; }
    public required string ScenarioId { get; set; }
    public required string PlayerId { get; set; }
}
