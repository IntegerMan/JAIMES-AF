namespace MattEland.Jaimes.Repositories.Entities;

public class Ruleset
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public ICollection<Player> Players { get; set; } = new List<Player>();
    public ICollection<Scenario> Scenarios { get; set; } = new List<Scenario>();
    public ICollection<Game> Games { get; set; } = new List<Game>();
}