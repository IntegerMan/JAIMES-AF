namespace MattEland.Jaimes.Domain;

public class RulesetDto
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
}