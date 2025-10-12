namespace MattEland.Jaimes.Repositories.Entities;

public class Game
{
    public Guid Id { get; set; }
    public required string RulesetId { get; set; }
    public required string ScenarioId { get; set; }
    public required string PlayerId { get; set; }
    public DateTime CreatedAt { get; set; }
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}
