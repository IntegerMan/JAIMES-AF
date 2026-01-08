namespace MattEland.Jaimes.ServiceDefinitions.Requests;

public class UpdateRulesetRequest
{
    public required string Name { get; set; }
    public string? Description { get; set; }
}