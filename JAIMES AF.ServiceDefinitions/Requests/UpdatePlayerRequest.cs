namespace MattEland.Jaimes.ServiceDefinitions.Requests;

public class UpdatePlayerRequest
{
    public required string RulesetId { get; set; }
    public string? Description { get; set; }
    public required string Name { get; set; }
}

